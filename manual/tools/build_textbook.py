from __future__ import annotations

import argparse
import html
import json
import re
import tempfile
from dataclasses import dataclass
from pathlib import Path

import cairosvg
from PIL import Image as PILImage, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Flowable,
    Frame,
    Image,
    KeepTogether,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)

ROOT = Path(__file__).resolve().parents[2]
CHAPTERS = ROOT / "manual" / "chapters"
FIGURES = ROOT / "manual" / "figures"
REFERENCES = ROOT / "manual" / "reference"
OUTPUT = ROOT / "manual" / "dist" / "RiskCase-Financial-Risk-Management-Engineering-Textbook.pdf"
SCREENSHOT_RE = re.compile(r"\[screenshot: ([^\]]+)\]")
FIGURE_RE = re.compile(r"\[figure: ([^\]]+)\]")
SOURCE_RE = re.compile(r"\[source: ([^#\]]+)#([^|\]]+)\|(\d+)\]")
CN_FONT = "RiskCaseYaHei"
CN_BOLD = "RiskCaseYaHeiBold"
CODE_FONT = "RiskCaseCode"
PAGE_W, PAGE_H = A4
MARGIN_X = 17 * mm
FRAME_Y = 15 * mm
FRAME_H = PAGE_H - 30 * mm
FRAME_W = PAGE_W - 2 * MARGIN_X


@dataclass
class SourceExcerpt:
    path: str
    start: int
    end: int
    lines: list[str]


def register_fonts() -> None:
    font_dir = Path(r"C:\Windows\Fonts")
    regular = font_dir / "msyh.ttc"
    bold = font_dir / "msyhbd.ttc"
    code = font_dir / "consola.ttf"
    missing = [str(path) for path in (regular, bold, code) if not path.exists()]
    if missing:
        raise FileNotFoundError("Required publication fonts are missing: " + ", ".join(missing))
    pdfmetrics.registerFont(TTFont(CN_FONT, str(regular), subfontIndex=0))
    pdfmetrics.registerFont(TTFont(CN_BOLD, str(bold), subfontIndex=0))
    pdfmetrics.registerFont(TTFont(CODE_FONT, str(code)))
    pdfmetrics.registerFontFamily(CN_FONT, normal=CN_FONT, bold=CN_BOLD, italic=CN_FONT, boldItalic=CN_BOLD)


def markdown_inline(value: str) -> str:
    escaped = html.escape(value, quote=False)
    escaped = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", escaped)
    escaped = re.sub(r"`([^`]+)`", rf'<font name="{CODE_FONT}" color="#117c78">\1</font>', escaped)
    return escaped


def read_chapters() -> list[dict]:
    result = []
    for path in sorted(CHAPTERS.glob("[0-9][0-9]-*.md")):
        text = path.read_text(encoding="utf-8")
        title_match = re.match(r"#\s+(\d{2})[｜|]\s*(.+)", text.splitlines()[0])
        if not title_match:
            raise ValueError(f"Chapter title must start with '# NN｜title': {path}")
        sections = []
        for match in re.finditer(r"^###\s+(.+?)\s*$", text, re.M):
            end = re.search(r"^###\s+", text[match.end():], re.M)
            content_end = match.end() + end.start() if end else len(text)
            sections.append({"heading": match.group(1), "body": text[match.end():content_end].strip()})
        if len(sections) != 5:
            raise ValueError(f"{path.name} must contain five teaching pages; found {len(sections)}")
        result.append({"path": path, "number": int(title_match.group(1)), "title": title_match.group(2).strip(), "sections": sections})
    if len(result) != 24:
        raise ValueError(f"Expected 24 editable chapters; found {len(result)}")
    return result


def source_excerpt(path: str, anchor: str, requested_lines: int) -> SourceExcerpt:
    source_path = ROOT / path
    if not source_path.exists():
        raise FileNotFoundError(f"Source reference does not exist: {path}")
    lines = source_path.read_text(encoding="utf-8").splitlines()
    matches = [index for index, line in enumerate(lines) if anchor in line]
    if not matches:
        raise ValueError(f"Source anchor '{anchor}' not found in {path}")
    start_index = matches[0]
    count = min(requested_lines, 22)
    selected = lines[start_index : start_index + count]
    return SourceExcerpt(path, start_index + 1, start_index + len(selected), selected)


def build_source_map(chapters: list[dict]) -> list[SourceExcerpt]:
    excerpts: list[SourceExcerpt] = []
    for chapter in chapters:
        for section in chapter["sections"]:
            for path, anchor, line_count in SOURCE_RE.findall(section["body"]):
                excerpt = source_excerpt(path.strip(), anchor.strip(), int(line_count))
                excerpts.append(excerpt)
    REFERENCES.mkdir(parents=True, exist_ok=True)
    lines = [
        "# 教材源码索引",
        "",
        "下表由 `manual/tools/build_textbook.py` 从本仓库实际源码锚点生成。行号对应当前源码；PDF 代码面板显示同一文件范围。",
        "",
        "| 章节 | 文件与行号 |",
        "|---|---|",
    ]
    index = 0
    for chapter in chapters:
        for section in chapter["sections"]:
            matches = SOURCE_RE.findall(section["body"])
            for path, anchor, _ in matches:
                excerpt = excerpts[index]
                index += 1
                lines.append(f"| {chapter['number']:02d}《{chapter['title']}》 | `{excerpt.path}:{excerpt.start}-{excerpt.end}`（`{anchor.strip()}`） |")
    (REFERENCES / "source-map.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return excerpts


def validate_assets(chapters: list[dict], allow_missing_screenshots: bool) -> list[str]:
    missing_screenshots: list[str] = []
    for chapter in chapters:
        for section in chapter["sections"]:
            body = section["body"]
            for name in FIGURE_RE.findall(body):
                if not (FIGURES / name).exists():
                    raise FileNotFoundError(f"Referenced figure missing: {name}")
            for name in SCREENSHOT_RE.findall(body):
                if not (FIGURES / name).exists():
                    missing_screenshots.append(name)
    if missing_screenshots and not allow_missing_screenshots:
        raise FileNotFoundError("Authentic Playwright screenshots are required: " + ", ".join(sorted(set(missing_screenshots))))
    return sorted(set(missing_screenshots))


def _font(name: str, size: float):
    return ParagraphStyle(name, fontName=CN_FONT, fontSize=size, leading=size * 1.5, textColor=colors.HexColor("#243447"))


class RiskCaseDocTemplate(BaseDocTemplate):
    def __init__(self, filename: str):
        super().__init__(filename, pagesize=A4, title="企业级金融风控系统：.NET 全栈架构、业务源码与数据一致性实践", author="RiskCase Engineering", subject="RiskCase 实际实现的中文插图技术教材", leftMargin=MARGIN_X, rightMargin=MARGIN_X, topMargin=17 * mm, bottomMargin=15 * mm)
        cover = Frame(0, 0, PAGE_W, PAGE_H, leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0, id="cover")
        body = Frame(MARGIN_X, FRAME_Y, FRAME_W, FRAME_H, leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0, id="body")
        self.addPageTemplates([
            PageTemplate(id="cover", frames=[cover], onPage=self._draw_cover_background),
            PageTemplate(id="body", frames=[body], onPage=self._draw_body_frame),
        ])

    @staticmethod
    def _draw_cover_background(canvas, doc):
        canvas.saveState()
        canvas.setFillColor(colors.HexColor("#f8fbfc"))
        canvas.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
        canvas.setFillColor(colors.HexColor("#117c78"))
        canvas.rect(0, 0, 11 * mm, PAGE_H, stroke=0, fill=1)
        canvas.setStrokeColor(colors.HexColor("#d7e8e6"))
        canvas.setLineWidth(1)
        for index in range(6):
            x = 120 + index * 61
            canvas.line(x, 190, x + 240, 470 + index * 18)
        canvas.restoreState()

    @staticmethod
    def _draw_body_frame(canvas, doc):
        canvas.saveState()
        canvas.setStrokeColor(colors.HexColor("#dbe5ea"))
        canvas.setLineWidth(0.55)
        canvas.line(MARGIN_X, PAGE_H - 11 * mm, PAGE_W - MARGIN_X, PAGE_H - 11 * mm)
        canvas.setFont(CN_BOLD, 7.5)
        canvas.setFillColor(colors.HexColor("#117c78"))
        canvas.drawString(MARGIN_X, PAGE_H - 8.4 * mm, "RISKCASE  /  VISUAL LEARNING EDITION")
        canvas.setFont(CN_FONT, 8)
        canvas.setFillColor(colors.HexColor("#708090"))
        canvas.drawString(MARGIN_X, 8.6 * mm, "合成演示数据 · 工程样例 · 非金融合规认证")
        canvas.drawRightString(PAGE_W - MARGIN_X, 8.6 * mm, f"{doc.page:03d}")
        canvas.restoreState()


class CoverPage(Flowable):
    def __init__(self):
        super().__init__()
        self.width = PAGE_W
        self.height = PAGE_H

    def draw(self):
        canvas = self.canv
        canvas.setFont(CN_BOLD, 12)
        canvas.setFillColor(colors.HexColor("#117c78"))
        canvas.drawString(56, PAGE_H - 105, "RISKCASE  ·  VISUAL LEARNING EDITION")
        canvas.setFont(CN_BOLD, 30)
        canvas.setFillColor(colors.HexColor("#20334a"))
        canvas.drawString(56, PAGE_H - 195, "企业级金融风控系统")
        canvas.setFont(CN_FONT, 19)
        canvas.drawString(56, PAGE_H - 237, ".NET 全栈架构、业务源码")
        canvas.drawString(56, PAGE_H - 267, "与数据一致性实践")
        canvas.setStrokeColor(colors.HexColor("#117c78"))
        canvas.setLineWidth(3)
        canvas.line(56, PAGE_H - 300, 195, PAGE_H - 300)
        canvas.setFont(CN_FONT, 11)
        canvas.setFillColor(colors.HexColor("#536678"))
        canvas.drawString(56, PAGE_H - 335, "从告警调查到独立复核：真实实现、真实源码与可复核工作流")
        canvas.setFont(CN_FONT, 10)
        canvas.setFillColor(colors.HexColor("#667788"))
        canvas.drawString(56, 68, "RiskCase Engineering  ·  2026  ·  Round 2")


class ChapterAnchor(Flowable):
    def __init__(self, key: str, title: str):
        super().__init__()
        self.key, self.title = key, title
        self.width = 0
        self.height = 0

    def draw(self):
        self.canv.bookmarkPage(self.key)
        self.canv.addOutlineEntry(self.title, self.key, level=0, closed=False)


class CodePanel(Flowable):
    TOKEN_RE = re.compile(r'("(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|//.*|\b(?:public|private|protected|internal|sealed|static|async|await|return|if|else|throw|new|var|record|class|using|namespace|Task|await|export|function|const|let|try|catch|from|import|return)\b)')

    def __init__(self, excerpt: SourceExcerpt, width: float):
        super().__init__()
        self.excerpt = excerpt
        self.width = width
        self.font_size = 7.25
        self.leading = 10.2
        self.rows: list[tuple[int | None, str]] = []
        self.height = 0

    def wrap(self, availWidth, availHeight):
        self.width = min(self.width, availWidth)
        text_width = self.width - 46
        self.rows = []
        for index, line in enumerate(self.excerpt.lines):
            pieces = self._wrap_line(line, text_width)
            for offset, piece in enumerate(pieces):
                self.rows.append((self.excerpt.start + index if offset == 0 else None, piece))
        self.height = max(42, len(self.rows) * self.leading + 30)
        return self.width, self.height

    def _wrap_line(self, line: str, width: float) -> list[str]:
        if not line:
            return [""]
        pieces, current = [], ""
        for char in line:
            candidate = current + char
            if current and pdfmetrics.stringWidth(candidate, CN_FONT, self.font_size) > width:
                pieces.append(current)
                current = "   " + char
            else:
                current = candidate
        pieces.append(current)
        return pieces

    def _colored_segments(self, value: str):
        position = 0
        keywords = {"public", "private", "protected", "internal", "sealed", "static", "async", "await", "return", "if", "else", "throw", "new", "var", "record", "class", "using", "namespace", "Task", "export", "function", "const", "let", "try", "catch", "from", "import"}
        for match in self.TOKEN_RE.finditer(value):
            if match.start() > position:
                yield value[position:match.start()], "#334155"
            token = match.group(0)
            if token.startswith(("\"", "'")):
                color = "#a15c13"
            elif token.startswith("//"):
                color = "#63816e"
            elif token in keywords:
                color = "#236a98"
            else:
                color = "#334155"
            yield token, color
            position = match.end()
        if position < len(value):
            yield value[position:], "#334155"

    def draw(self):
        canvas = self.canv
        canvas.saveState()
        canvas.setFillColor(colors.HexColor("#f4f7f9"))
        canvas.setStrokeColor(colors.HexColor("#dbe5ea"))
        canvas.roundRect(0, 0, self.width, self.height, 7, stroke=1, fill=1)
        canvas.setFont(CN_FONT, 7.0)
        canvas.setFillColor(colors.HexColor("#526273"))
        canvas.drawString(10, self.height - 13, f"{self.excerpt.path}  ·  L{self.excerpt.start}–{self.excerpt.end}")
        y = self.height - 27
        for number, line in self.rows:
            if number is not None:
                canvas.setFillColor(colors.HexColor("#97a6b4"))
                canvas.setFont(CODE_FONT, self.font_size)
                canvas.drawRightString(35, y, str(number))
            x = 42
            canvas.setFont(CN_FONT, self.font_size)
            for text, color in self._colored_segments(line):
                canvas.setFillColor(colors.HexColor(color))
                canvas.drawString(x, y, text)
                x += pdfmetrics.stringWidth(text, CN_FONT, self.font_size)
            y -= self.leading
        canvas.restoreState()


class Callout(Flowable):
    def __init__(self, paragraph: Paragraph, width: float, background="#eaf5f3", accent="#117c78"):
        super().__init__()
        self.paragraph = paragraph
        self.width = width
        self.background = colors.HexColor(background)
        self.accent = colors.HexColor(accent)
        self.height = 0

    def wrap(self, availWidth, availHeight):
        self.width = min(self.width, availWidth)
        _, self.child_height = self.paragraph.wrap(self.width - 24, availHeight)
        self.height = self.child_height + 18
        return self.width, self.height

    def draw(self):
        self.canv.saveState()
        self.canv.setFillColor(self.background)
        self.canv.roundRect(0, 0, self.width, self.height, 7, fill=1, stroke=0)
        self.canv.setFillColor(self.accent)
        self.canv.roundRect(0, 0, 3.5, self.height, 2, fill=1, stroke=0)
        self.paragraph.drawOn(self.canv, 12, 9)
        self.canv.restoreState()


def _fit_image(path: Path, width: float, max_height: float, temp_dir: Path) -> Image:
    target = path
    if path.suffix.lower() == ".svg":
        target = temp_dir / (path.stem + ".png")
        cairosvg.svg2png(url=str(path), write_to=str(target), output_width=1800)
    image = Image(str(target))
    image._restrictSize(width, max_height)
    image.hAlign = "CENTER"
    return image


def _annotated_screenshot(source: Path, destination: Path, labels: list[dict]) -> Path:
    image = PILImage.open(source).convert("RGBA")
    overlay = PILImage.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    try:
        font = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", max(18, round(image.width * 0.018)))
    except OSError:
        font = ImageFont.load_default()
    radius = max(18, round(image.width * 0.022))
    for index, item in enumerate(labels, 1):
        x, y = round(item["x"] * image.width), round(item["y"] * image.height)
        color = (17, 124, 120, 235)
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color, outline=(255, 255, 255, 245), width=max(2, radius // 6))
        label = str(item.get("number", index))
        box = draw.textbbox((0, 0), label, font=font)
        tw, th = box[2] - box[0], box[3] - box[1]
        draw.text((x - tw / 2, y - th / 2 - box[1]), label, font=font, fill=(255, 255, 255, 255))
    image = PILImage.alpha_composite(image, overlay).convert("RGB")
    image.save(destination, quality=94)
    return destination


def _paragraphs_for_block(block: str, styles: dict, width: float, temp_dir: Path):
    source_match = SOURCE_RE.fullmatch(block.strip())
    if source_match:
        path, anchor, line_count = source_match.groups()
        excerpt = source_excerpt(path.strip(), anchor.strip(), int(line_count))
        yield CodePanel(excerpt, width)
        return
    figure_match = FIGURE_RE.fullmatch(block.strip())
    if figure_match:
        name = figure_match.group(1)
        path = FIGURES / name
        yield _fit_image(path, width, 314, temp_dir)
        yield Spacer(1, 5)
        try:
            data = json.loads((FIGURES / "diagrams.json").read_text(encoding="utf-8"))
            caption = next(item["title"] for item in data["figures"] if item["file"] == name)
        except (StopIteration, KeyError):
            caption = name
        yield Paragraph(f"图｜{markdown_inline(caption)}。原理图为 RiskCase 原创矢量图。", styles["caption"])
        return
    screenshot_match = SCREENSHOT_RE.fullmatch(block.strip())
    if screenshot_match:
        name = screenshot_match.group(1)
        path = FIGURES / name
        if not path.exists():
            raise FileNotFoundError(f"Missing authentic application screenshot {path}")
        annotations_path = FIGURES / "screenshot-callouts.json"
        annotation_data = json.loads(annotations_path.read_text(encoding="utf-8")) if annotations_path.exists() else {}
        labels = annotation_data.get(name, [])
        if labels:
            annotated = temp_dir / f"annotated-{name}"
            _annotated_screenshot(path, annotated, labels)
        else:
            annotated = path
        yield _fit_image(annotated, width, 355, temp_dir)
        yield Spacer(1, 4)
        legend = " · ".join(f"{i}. {item['label']}" for i, item in enumerate(labels, 1))
        caption = "真实应用截图 · Playwright + PostgreSQL 合成数据"
        if legend:
            caption += f"\n{legend}"
        yield Paragraph(markdown_inline(caption).replace("\n", "<br/>"), styles["caption"])
        return
    rows = [line.strip() for line in block.strip().splitlines() if line.strip()]
    if rows and all(line.startswith("- ") or re.match(r"\d+\. ", line) for line in rows):
        items = []
        for row in rows:
            text = re.sub(r"^(?:- |\d+\. )", "", row)
            items.append(Paragraph(markdown_inline(text), styles["bullet"]))
        from reportlab.platypus import ListFlowable, ListItem

        list_items = [ListItem(item, leftIndent=3, bulletColor=colors.HexColor("#117c78")) for item in items]
        yield ListFlowable(list_items, bulletType="bullet", start="circle", leftIndent=16, bulletFontName=CN_FONT, bulletFontSize=7, spaceAfter=7)
        return
    text = markdown_inline(" ".join(rows))
    if text.startswith("<b>练习</b>"):
        text = text.replace("<b>练习</b>", '<font color="#117c78"><b>练习</b></font>', 1)
        yield Callout(Paragraph(text, styles["body"]), width, background="#eef5f8", accent="#22364f")
    elif text.startswith("<b>小结</b>") or text.startswith("<b>全书回顾</b>"):
        text = re.sub(r"^<b>(.+?)</b>", r'<font color="#117c78"><b>\1</b></font>', text, count=1)
        yield Callout(Paragraph(text, styles["body"]), width)
    else:
        yield Paragraph(text, styles["body"])


def chapter_flowables(chapters: list[dict], styles: dict, temp_dir: Path):
    story = []
    for chapter in chapters:
        story.append(PageBreak())
        chapter_key = f"chapter-{chapter['number']:02d}"
        story.append(ChapterAnchor(chapter_key, f"第 {chapter['number']:02d} 章｜{chapter['title']}"))
        for page_index, section in enumerate(chapter["sections"]):
            if page_index:
                story.append(PageBreak())
            story.append(Paragraph(f"第 {chapter['number']:02d} 章　·　第 {page_index + 1} 页 / 5", styles["kicker"]))
            story.append(Paragraph(markdown_inline(chapter["title"]), styles["chapterName"]))
            heading = re.sub(r"^第\s*\d+\s*页[｜|]\s*", "", section["heading"])
            story.append(Paragraph(markdown_inline(heading), styles["pageTitle"]))
            story.append(Spacer(1, 8))
            blocks = [value.strip() for value in re.split(r"\n\s*\n", section["body"]) if value.strip()]
            for block in blocks:
                for flowable in _paragraphs_for_block(block, styles, FRAME_W, temp_dir):
                    story.append(flowable)
                    story.append(Spacer(1, 7))
    return story


def build_styles() -> dict:
    styles = {
        "body": ParagraphStyle("BodyCN", fontName=CN_FONT, fontSize=10.2, leading=16.4, textColor=colors.HexColor("#293b4d"), spaceAfter=4, wordWrap="CJK"),
        "bullet": ParagraphStyle("BulletCN", fontName=CN_FONT, fontSize=9.5, leading=14.2, textColor=colors.HexColor("#293b4d"), spaceAfter=3, wordWrap="CJK"),
        "caption": ParagraphStyle("CaptionCN", fontName=CN_FONT, fontSize=8.2, leading=12, textColor=colors.HexColor("#687888"), alignment=TA_CENTER, wordWrap="CJK"),
        "kicker": ParagraphStyle("Kicker", fontName=CN_BOLD, fontSize=8.5, leading=12, textColor=colors.HexColor("#117c78"), spaceAfter=2),
        "chapterName": ParagraphStyle("ChapterName", fontName=CN_FONT, fontSize=9, leading=13, textColor=colors.HexColor("#748494"), spaceAfter=4),
        "pageTitle": ParagraphStyle("PageTitle", fontName=CN_BOLD, fontSize=19, leading=26, textColor=colors.HexColor("#20334a"), spaceAfter=7, wordWrap="CJK"),
        "frontTitle": ParagraphStyle("FrontTitle", fontName=CN_BOLD, fontSize=23, leading=30, textColor=colors.HexColor("#20334a"), spaceAfter=12, wordWrap="CJK"),
        "frontSub": ParagraphStyle("FrontSub", fontName=CN_BOLD, fontSize=14, leading=21, textColor=colors.HexColor("#117c78"), spaceAfter=12, wordWrap="CJK"),
        "toc": ParagraphStyle("TOC", fontName=CN_FONT, fontSize=10.1, leading=17, textColor=colors.HexColor("#293b4d"), leftIndent=0, spaceAfter=3, wordWrap="CJK"),
        "tableHead": ParagraphStyle("TableHead", fontName=CN_BOLD, fontSize=9, leading=13, textColor=colors.white, wordWrap="CJK"),
        "tableCell": ParagraphStyle("TableCell", fontName=CN_FONT, fontSize=8.4, leading=12, textColor=colors.HexColor("#293b4d"), wordWrap="CJK"),
        "small": ParagraphStyle("SmallCN", fontName=CN_FONT, fontSize=8.8, leading=13.2, textColor=colors.HexColor("#526273"), wordWrap="CJK"),
    }
    return styles


def front_matter(chapters: list[dict], styles: dict):
    story: list = [CoverPage(),]
    from reportlab.platypus import NextPageTemplate

    story.extend([NextPageTemplate("body"), PageBreak()])
    story.extend([
        Paragraph("版权、数据与使用边界", styles["frontTitle"]),
        Paragraph("本教材讲解 RiskCase 当前仓库中的真实实现。业务名称、账号、交易和告警均为合成演示数据；示例不构成法律、金融或监管建议。RiskCase 不执行付款、冻结账户、改变余额，也没有取得金融合规认证。", styles["body"]),
        Spacer(1, 10),
        Paragraph("本版依据 Round 2 工程提交 df126c7 修订。评估、复核冻结与幂等恢复的说明对应该实现；完整修复与验证记录保存在 docs/round-2。", styles["body"]),
        Spacer(1, 10),
        Paragraph("代码许可与模板来源", styles["frontSub"]),
        Paragraph("RiskCase 源码采用仓库根目录声明的 Apache-2.0 许可。后端从 Jason Taylor CleanArchitecture v10.8.0 实际生成解决方案改造；上游 MIT 许可证原文及准确提交 SHA 保存在 manual/reference。依赖许可应以各锁定版本的包元数据为准。", styles["body"]),
        Spacer(1, 10),
        Paragraph("如何阅读", styles["frontSub"]),
        Paragraph("每章先从业务场景提出问题，再用机制图说明边界，接着引用本仓库实际源码，之后推演运行路径，最后通过问题和小结巩固理解。源码行号由出版脚本从当前文件定位。图谱截图来自真实前后端和 PostgreSQL 合成种子；SVG 原理图是可编辑的 RiskCase 专用矢量图。", styles["body"]),
        Spacer(1, 18),
        Callout(Paragraph("阅读限制：分数与信号只提示调查方向；共享交易对手不证明客户互相关联；记录处置不代表银行已经采取外部行动。", styles["body"]), FRAME_W, background="#fff4df", accent="#b87414"),
        PageBreak(),
        Paragraph("导读｜如何读懂这套系统", styles["frontTitle"]),
        Paragraph("本书面向后端与前端工程师、测试人员、业务分析员，以及希望理解一条真实跨层工作流的读者。阅读时可以先看机制图，再沿着文件路径打开源码；如果某页提出问题，先按当前实现回答，再比较其他设计可能带来的代价。", styles["body"]),
        Spacer(1, 8),
        Paragraph("全书分为四部分：风险业务模型；.NET 后端架构与数据一致性；React 调查工作区；测试、故障恢复和工程取舍。每章最后的练习留给读者完成，答案需要回到业务场景和源代码中论证。", styles["body"]),
        Spacer(1, 12),
        Callout(Paragraph("源码定位方法：打开节选标题中显示的仓库相对路径与行号。Markdown 章节和 SVG 原理图均可编辑，PDF 可从仓库工具重建。", styles["body"]), FRAME_W),
        PageBreak(),
        Paragraph("内容目录", styles["frontTitle"]),
    ])
    for index, chapter in enumerate(chapters[:12]):
        page_number = 6 + index * 5
        title = html.escape(chapter["title"])
        story.append(Paragraph(f'<link href="#chapter-{chapter["number"]:02d}" color="#22364f">{chapter["number"]:02d}　{title}</link><font color="#718096">　{page_number}</font>', styles["toc"]))
    story.append(PageBreak())
    story.append(Paragraph("内容目录　·　续", styles["frontTitle"]))
    for index, chapter in enumerate(chapters[12:], start=12):
        page_number = 6 + index * 5
        title = html.escape(chapter["title"])
        story.append(Paragraph(f'<link href="#chapter-{chapter["number"]:02d}" color="#22364f">{chapter["number"]:02d}　{title}</link><font color="#718096">　{page_number}</font>', styles["toc"]))
    return story


GLOSSARY = [
    ("Aggregate / 聚合", "围绕必须共同保持一致的领域规则组织的一组对象。RiskCase 聚合负责案件生命周期与调查写入。"),
    ("Anti-forgery / 反伪造", "浏览器 Cookie 写入旁的来源校验。RiskCase 使用 CSRF request token 防止跨站请求伪造。"),
    ("Audit event / 审计事件", "记录重要业务操作、责任人与版本的持久化事实，不等同于诊断日志。"),
    ("CQRS", "将命令写入与查询读取按用途组织；本项目没有因此分拆数据库或部署服务。"),
    ("Change Tracker", "EF Core 用于记录实体快照和状态，以生成相应 SQL 写入。"),
    ("CSRF", "跨站请求伪造；利用浏览器自动附带身份 Cookie 的攻击方式。"),
    ("ETag / If-Match", "HTTP 条件写入机制；此处 If-Match 携带案件版本，避免过期页面覆盖新状态。"),
    ("Idempotency", "相同业务意图可安全重试并取得原结果；键复用到不同内容时应冲突。"),
    ("MVCC", "PostgreSQL 多版本并发控制，为事务提供一致可见的数据版本。"),
    ("MediatR pipeline", "围绕请求 handler 执行的行为链；可集中处理授权、验证、日志与幂等。"),
    ("NSwag", "根据 OpenAPI 文档生成 TypeScript 客户端与数据类型的工具。"),
    ("Optimistic concurrency", "假定写冲突较少，用版本条件检测并拒绝基于旧状态提交的写入。"),
    ("OpenAPI", "描述 HTTP API 路由、参数、请求体和响应结构的机器可读契约。"),
    ("PostgreSQL advisory lock", "由应用显式使用的事务级互斥锁；此处用于同用户和幂等键的协调。"),
    ("React Flow", "React 节点与边图形交互库；RiskCase 用它浏览已保存的调查关系。"),
    ("TanStack Query", "管理服务器请求缓存、加载状态、失效与 mutation 的前端工具。"),
]


def appendix_flowables(chapters: list[dict], excerpts: list[SourceExcerpt], styles: dict):
    story = [PageBreak(), ChapterAnchor("appendix-glossary", "附录 A｜术语表"), Paragraph("附录 A｜关键术语", styles["frontTitle"])]
    story.append(Paragraph("术语释义依照本项目实际用法，不延伸为对所有技术栈的统一定义。", styles["small"]))
    data = [[Paragraph("术语", styles["tableHead"]), Paragraph("本书语境中的含义", styles["tableHead"])]]
    data.extend([[Paragraph(markdown_inline(term), styles["tableCell"]), Paragraph(markdown_inline(definition), styles["tableCell"])] for term, definition in GLOSSARY])
    table = Table(data, colWidths=[105, FRAME_W - 105], repeatRows=1, hAlign="LEFT")
    table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#22364f")),
        ("BACKGROUND", (0, 1), (-1, -1), colors.white),
        ("GRID", (0, 0), (-1, -1), 0.45, colors.HexColor("#dbe5ea")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("LEFTPADDING", (0, 0), (-1, -1), 7),
        ("RIGHTPADDING", (0, 0), (-1, -1), 7),
        ("TOPPADDING", (0, 0), (-1, -1), 6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
    ]))
    story.extend([Spacer(1, 8), table, PageBreak(), ChapterAnchor("appendix-source-map", "附录 B｜源码索引"), Paragraph("附录 B｜源码与章节索引", styles["frontTitle"]), Paragraph("PDF 中的源码节选来自以下实际文件。每个节选都同时印出相对仓库路径与实际起止行。", styles["small"])])
    rows = [[Paragraph("章节", styles["tableHead"]), Paragraph("源码路径与行号", styles["tableHead"])]]
    index = 0
    for chapter in chapters:
        for section in chapter["sections"]:
            for _ in SOURCE_RE.findall(section["body"]):
                excerpt = excerpts[index]
                index += 1
                rows.append([Paragraph(f"{chapter['number']:02d}　{markdown_inline(chapter['title'])}", styles["tableCell"]), Paragraph(f"<font name=\"{CODE_FONT}\">{html.escape(excerpt.path)}:{excerpt.start}-{excerpt.end}</font>", styles["tableCell"])
                ])
    ref_table = Table(rows, colWidths=[175, FRAME_W - 175], repeatRows=1, hAlign="LEFT")
    ref_table.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#22364f")),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#dbe5ea")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#f7fafb")]),
        ("LEFTPADDING", (0, 0), (-1, -1), 6), ("RIGHTPADDING", (0, 0), (-1, -1), 6),
        ("TOPPADDING", (0, 0), (-1, -1), 4), ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    story.append(Spacer(1, 7))
    story.append(ref_table)
    return story


def main() -> None:
    parser = argparse.ArgumentParser(description="Build the editable RiskCase engineering textbook PDF.")
    parser.add_argument("--validate-only", action="store_true", help="Validate Markdown, figures and source anchors without writing a PDF.")
    parser.add_argument("--allow-missing-screenshots", action="store_true", help="Only for pre-publication checks before Playwright screenshots exist.")
    args = parser.parse_args()

    chapters = read_chapters()
    missing_screenshots = validate_assets(chapters, args.allow_missing_screenshots)
    excerpts = build_source_map(chapters)
    if args.validate_only:
        print(f"Validated {len(chapters)} chapters, 24 original SVG references, {len(excerpts)} source excerpts.")
        if missing_screenshots:
            print("Awaiting authentic screenshots: " + ", ".join(missing_screenshots))
        return

    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="riskcase-book-") as temp_name:
        temp_dir = Path(temp_name)
        styles = build_styles()
        story = front_matter(chapters, styles)
        story.extend(chapter_flowables(chapters, styles, temp_dir))
        story.extend(appendix_flowables(chapters, excerpts, styles))
        doc = RiskCaseDocTemplate(str(OUTPUT))
        doc.build(story)
    print(f"Published {OUTPUT}")


if __name__ == "__main__":
    main()
