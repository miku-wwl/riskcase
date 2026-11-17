from __future__ import annotations

import argparse
import html
import io
import json
import re
import tempfile
import functools
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
from reportlab.pdfgen import canvas as pdfcanvas
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
ENGINEERING_SHA = "2177004a39a534ee1796b8391a590763cce62fc2"
PAGE_MAP: dict[str, int] = {}
FIGURE_NUMBERS: dict[str, str] = {}
LAYOUT_DECISIONS: dict[int, dict] = {}
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
    escaped = re.sub(r"\[([^\]]+)\]\((https://[^\s)]+)\)", r'<link href="\2" color="#117c78">\1</link>', escaped)
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
        if not sections:
            raise ValueError(f"{path.name} must contain teaching sections")
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
    if len(matches) != 1:
        raise ValueError(f"Source anchor '{anchor}' must be unique in {path}; found {len(matches)}")
    start_index = matches[0]
    count = requested_lines
    if count < 1 or start_index + count > len(lines):
        raise ValueError(f"Source excerpt is out of bounds: {path} / {anchor} / {count}")
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
        super().__init__(filename, pagesize=A4, invariant=1, title="企业级金融风控系统：.NET 全栈架构、业务源码与数据一致性实践", author="RiskCase Engineering", subject="Round 4 图解精修版 · 工程基准 " + ENGINEERING_SHA, leftMargin=MARGIN_X, rightMargin=MARGIN_X, topMargin=17 * mm, bottomMargin=15 * mm)
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
        canvas.drawString(56, 68, "RiskCase Engineering  ·  2026  ·  Round 4 图解精修版")


class ChapterAnchor(Flowable):
    def __init__(self, key: str, title: str):
        super().__init__()
        self.key, self.title = key, title
        self.width = 0
        self.height = 0

    def draw(self):
        PAGE_MAP[self.key] = self.canv.getPageNumber()
        self.canv.bookmarkPage(self.key)
        self.canv.addOutlineEntry(self.title, self.key, level=0, closed=False)


class CodePanel(Flowable):
    TOKEN_RE = re.compile(r'("(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)*\'|//.*|\b(?:public|private|protected|internal|sealed|static|async|await|return|if|else|throw|new|var|record|class|using|namespace|Task|await|export|function|const|let|try|catch|from|import|return)\b)')

    def __init__(self, excerpt: SourceExcerpt, width: float):
        super().__init__()
        self.excerpt = excerpt
        self.width = width
        self.font_size = 8.7
        self.leading = 12.2
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
        self.height = max(58, len(self.rows) * self.leading + 43)
        return self.width, self.height

    @staticmethod
    def _mixed_width(text: str, size: float) -> float:
        return sum(pdfmetrics.stringWidth(char, CODE_FONT if ord(char) < 128 else CN_FONT, size) for char in text)

    def _wrap_line(self, line: str, width: float) -> list[str]:
        if not line:
            return [""]
        pieces, current = [], ""
        # Keep Latin identifiers intact. Chinese and punctuation provide legal wrap points.
        tokens = re.findall(r'''"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|[A-Za-z_][A-Za-z_0-9]*|[0-9]+|[^\x00-\x7f]|.''', line)
        expanded = []
        for token in tokens:
            if self._mixed_width(token, self.font_size) > width and token.startswith(('"', "'")):
                expanded.extend(re.findall(r"[A-Za-z_][A-Za-z_0-9]*|[0-9]+|[^\x00-\x7f]|.", token))
            else:
                expanded.append(token)
        tokens = expanded
        for token in tokens:
            candidate = current + token
            if current and self._mixed_width(candidate, self.font_size) > width:
                pieces.append(current)
                current = "    " + token
            else:
                current = candidate
            if self._mixed_width(current, self.font_size) > width:
                raise ValueError(f"Unbreakable code token in {self.excerpt.path}: {token}")
        pieces.append(current)
        return pieces

    def split(self, availWidth, availHeight):
        self.wrap(availWidth, availHeight)
        used = 43
        count = 0
        for line in self.excerpt.lines:
            cost = len(self._wrap_line(line, self.width - 46)) * self.leading
            if used + cost > availHeight:
                break
            used += cost
            count += 1
        if count < 2 or count >= len(self.excerpt.lines):
            return []
        first = SourceExcerpt(self.excerpt.path, self.excerpt.start, self.excerpt.start + count - 1, self.excerpt.lines[:count])
        rest = SourceExcerpt(self.excerpt.path, self.excerpt.start + count, self.excerpt.end, self.excerpt.lines[count:])
        return [CodePanel(first, self.width), CodePanel(rest, self.width)]

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
        canvas.setFont(CN_FONT, 7.2)
        canvas.setFillColor(colors.HexColor("#526273"))
        source_url = f"https://github.com/miku-wwl/riskcase/blob/{ENGINEERING_SHA}/{self.excerpt.path}#L{self.excerpt.start}"
        canvas.drawString(10, self.height - 13, self.excerpt.path)
        canvas.linkURL(source_url, (8, self.height - 18, self.width - 8, self.height - 2), relative=1)
        canvas.setFont(CN_FONT, 6.7)
        canvas.drawString(10, self.height - 25, f"L{self.excerpt.start}–{self.excerpt.end}　·　> 表示同一源码行的排版续行")
        y = self.height - 39
        for number, line in self.rows:
            if number is not None:
                canvas.setFillColor(colors.HexColor("#97a6b4"))
                canvas.setFont(CODE_FONT, self.font_size)
                canvas.drawRightString(35, y, str(number))
            else:
                canvas.setFillColor(colors.HexColor("#97a6b4"))
                canvas.setFont(CODE_FONT, 7)
                canvas.drawRightString(35, y, ">")
            x = 42
            for text, color in self._colored_segments(line):
                canvas.setFillColor(colors.HexColor(color))
                for run in re.findall(r"[\x00-\x7f]+|[^\x00-\x7f]+", text):
                    face = CODE_FONT if ord(run[0]) < 128 else CN_FONT
                    canvas.setFont(face, self.font_size)
                    canvas.drawString(x, y, run)
                    x += pdfmetrics.stringWidth(run, face, self.font_size)
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
        yield _fit_image(path, width, 320, temp_dir)
        yield Spacer(1, 5)
        try:
            data = json.loads((FIGURES / "diagrams.json").read_text(encoding="utf-8"))
            caption = next(item["title"] for item in data["figures"] if item["file"] == name)
        except (StopIteration, KeyError):
            extra_captions = json.loads((FIGURES / "round3-captions.json").read_text(encoding="utf-8"))
            caption = extra_captions.get(name, name)
        revised = json.loads((FIGURES / "publication-captions.json").read_text(encoding="utf-8")) if (FIGURES / "publication-captions.json").exists() else {}
        caption = revised.get(name, caption)
        yield Paragraph(f"图 {FIGURE_NUMBERS.get(name, '')}｜{markdown_inline(caption)}", styles["caption"])
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
        revised = json.loads((FIGURES / "publication-captions.json").read_text(encoding="utf-8")) if (FIGURES / "publication-captions.json").exists() else {}
        caption = f"图 {FIGURE_NUMBERS.get(name, '')}｜" + revised.get(name, "真实应用截图 · Playwright + PostgreSQL 合成数据")
        if legend:
            caption += f"\n{legend}"
        yield Paragraph(markdown_inline(caption).replace("\n", "<br/>"), styles["caption"])
        return
    rows = [line.strip() for line in block.strip().splitlines() if line.strip()]
    if rows and rows[0].startswith("|"):
        cells = [[cell.strip() for cell in row.strip("|").split("|")] for row in rows if not re.match(r"^\|[\s:|\-]+\|$", row)]
        count = len(cells[0])
        data = [[Paragraph(markdown_inline(cell), styles["tableHead"] if i == 0 else styles["tableCell"]) for cell in row] for i, row in enumerate(cells)]
        widths = [width / count] * count
        if count == 2:
            widths = [width * .28, width * .72]
        table = Table(data, colWidths=widths, repeatRows=1, hAlign="LEFT")
        table.setStyle(TableStyle([
            ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#22364f")),
            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#f1f7f7")]),
            ("GRID", (0, 0), (-1, -1), .4, colors.HexColor("#dbe5ea")),
            ("VALIGN", (0, 0), (-1, -1), "TOP"),
            ("LEFTPADDING", (0, 0), (-1, -1), 7), ("RIGHTPADDING", (0, 0), (-1, -1), 7),
            ("TOPPADDING", (0, 0), (-1, -1), 6), ("BOTTOMPADDING", (0, 0), (-1, -1), 6),
        ]))
        yield table
        return
    if rows and all(line.startswith("- ") or re.match(r"\d+\. ", line) for line in rows):
        items = []
        for row in rows:
            text = re.sub(r"^(?:- |\d+\. )", "", row)
            items.append(Paragraph(markdown_inline(text), styles["bullet"]))
        from reportlab.platypus import ListFlowable, ListItem

        list_items = [ListItem(item, leftIndent=3, bulletColor=colors.HexColor("#117c78")) for item in items]
        ordered = bool(re.match(r"\d+\. ", rows[0]))
        yield ListFlowable(list_items, bulletType="1" if ordered else "bullet", start=1 if ordered else "circle", leftIndent=18, bulletFontName=CN_FONT, bulletFontSize=9 if ordered else 7, spaceAfter=7)
        return
    text = markdown_inline(" ".join(rows))
    if text in ("<b>练习</b>", "<b>参考解</b>"):
        yield Paragraph(text, styles["subTitle"])
    elif text.startswith("<b>练习</b>"):
        text = text.replace("<b>练习</b>", '<font color="#117c78"><b>练习</b></font>', 1)
        yield Callout(Paragraph(text, styles["body"]), width, background="#eef5f8", accent="#22364f")
    elif text.startswith("<b>小结</b>") or text.startswith("<b>全书回顾</b>"):
        text = re.sub(r"^<b>(.+?)</b>", r'<font color="#117c78"><b>\1</b></font>', text, count=1)
        yield Callout(Paragraph(text, styles["body"]), width)
    elif any(text.startswith(f"<b>{label}</b>") for label in ("已知", "问题", "判读", "参考解", "常见误区", "适用边界")):
        yield Callout(Paragraph(text, styles["body"]), width, background="#fff6e5" if text.startswith("<b>常见误区") else "#eef5f8", accent="#b87414" if text.startswith("<b>常见误区") else "#22364f")
    else:
        yield Paragraph(text, styles["body"])


def chapter_flowables(chapters: list[dict], styles: dict, temp_dir: Path):
    story = []
    measure_canvas = pdfcanvas.Canvas(io.BytesIO(), initialFontName=CN_FONT)
    for chapter in chapters:
        story.append(PageBreak())
        chapter_key = f"chapter-{chapter['number']:02d}"
        story.append(ChapterAnchor(chapter_key, f"第 {chapter['number']:02d} 章｜{chapter['title']}"))
        section_bodies = []
        for section in chapter["sections"]:
            body = []
            for block in [value.strip() for value in re.split(r"\n\s*\n", section["body"].replace("<!-- page -->", "")) if value.strip()]:
                for item in _paragraphs_for_block(block, styles, FRAME_W, temp_dir):
                    body.extend([item, Spacer(1, 7)])
            section_bodies.append(body)
        def occupied_height(items):
            return sum(item.wrapOn(measure_canvas, FRAME_W, FRAME_H)[1] + item.getSpaceBefore() + item.getSpaceAfter() for item in items)
        # Join worked example and consolidation only when actual content fits comfortably.
        # This avoids imposing another fixed page quota on the 24 chapters.
        combined_height = occupied_height(section_bodies[-2]) + occupied_height(section_bodies[-1]) + 100
        join_exercises = combined_height <= FRAME_H - 40
        LAYOUT_DECISIONS[chapter["number"]] = {"join_worked_example_and_exercises": join_exercises, "estimated_points": round(combined_height, 1), "limit_points": round(FRAME_H - 40, 1)}
        for page_index, section in enumerate(chapter["sections"]):
            new_page = section["body"].startswith("<!-- page -->")
            if page_index == len(chapter["sections"]) - 1 and join_exercises:
                new_page = False
            if page_index and new_page:
                story.append(PageBreak())
            if page_index == 0 or new_page:
                story.append(Paragraph(f"第 {chapter['number']:02d} 章　｜　{markdown_inline(chapter['title'])}", styles["kicker"]))
            heading = re.sub(r"^第\s*\d+\s*页[｜|]\s*", "", section["heading"])
            story.append(SectionAnchor(f"section-{chapter['number']:02d}-{page_index + 1}"))
            story.append(Paragraph(markdown_inline(heading), styles["pageTitle"] if page_index == 0 or new_page else styles["subTitle"]))
            story.append(Spacer(1, 5))
            story.extend(section_bodies[page_index])
    return story


class SectionAnchor(Flowable):
    def __init__(self, key):
        super().__init__()
        self.key = key
        self.width = self.height = 0

    def draw(self):
        PAGE_MAP[self.key] = self.canv.getPageNumber()


class TocRow(Flowable):
    def __init__(self, key, label):
        super().__init__()
        self.key, self.label = key, label
        self.width, self.height = FRAME_W, 27

    def draw(self):
        canvas = self.canv
        canvas.setFillColor(colors.HexColor("#293b4d"))
        canvas.setFont(CN_FONT, 10.2)
        canvas.drawString(0, 11, self.label)
        canvas.linkRect("", self.key, (0, 0, self.width, self.height), relative=1, thickness=0)
        label_width = pdfmetrics.stringWidth(self.label, CN_FONT, 10.2)
        canvas.setDash(1, 3)
        canvas.setStrokeColor(colors.HexColor("#beced8"))
        canvas.line(label_width + 10, 11, self.width - 30, 11)
        canvas.setDash()
        canvas.setFont(CODE_FONT, 10.2)
        canvas.drawRightString(self.width, 11, str(PAGE_MAP.get(self.key, "")))


def build_styles() -> dict:
    styles = {
        "body": ParagraphStyle("BodyCN", fontName=CN_FONT, fontSize=10.2, leading=16.4, textColor=colors.HexColor("#293b4d"), spaceAfter=4, wordWrap="CJK"),
        "bullet": ParagraphStyle("BulletCN", fontName=CN_FONT, fontSize=9.5, leading=14.2, textColor=colors.HexColor("#293b4d"), spaceAfter=3, wordWrap="CJK"),
        "caption": ParagraphStyle("CaptionCN", fontName=CN_FONT, fontSize=8.2, leading=12, textColor=colors.HexColor("#687888"), alignment=TA_CENTER, wordWrap="CJK"),
        "kicker": ParagraphStyle("Kicker", fontName=CN_BOLD, fontSize=8.5, leading=12, textColor=colors.HexColor("#117c78"), spaceAfter=2),
        "chapterName": ParagraphStyle("ChapterName", fontName=CN_FONT, fontSize=9, leading=13, textColor=colors.HexColor("#748494"), spaceAfter=4),
        "pageTitle": ParagraphStyle("PageTitle", fontName=CN_BOLD, fontSize=18, leading=25, textColor=colors.HexColor("#20334a"), spaceAfter=7, wordWrap="CJK", splitLongWords=1, keepWithNext=True),
        "subTitle": ParagraphStyle("SubTitle", fontName=CN_BOLD, fontSize=12.5, leading=19, textColor=colors.HexColor("#117c78"), spaceBefore=8, spaceAfter=5, wordWrap="CJK", keepWithNext=True),
        "frontTitle": ParagraphStyle("FrontTitle", fontName=CN_BOLD, fontSize=23, leading=30, textColor=colors.HexColor("#20334a"), spaceAfter=12, wordWrap="CJK"),
        "frontSub": ParagraphStyle("FrontSub", fontName=CN_BOLD, fontSize=14, leading=21, textColor=colors.HexColor("#117c78"), spaceAfter=12, wordWrap="CJK"),
        "toc": ParagraphStyle("TOC", fontName=CN_FONT, fontSize=10.1, leading=17, textColor=colors.HexColor("#293b4d"), leftIndent=0, spaceAfter=3, wordWrap="CJK"),
        "tableHead": ParagraphStyle("TableHead", fontName=CN_BOLD, fontSize=9, leading=13, textColor=colors.white, wordWrap="CJK"),
        "tableCell": ParagraphStyle("TableCell", fontName=CN_FONT, fontSize=8.8, leading=13.2, textColor=colors.HexColor("#293b4d"), wordWrap="CJK"),
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
        Paragraph(f"本版为 Round 4 图解精修版。全部应用机制以固定工程基准 {ENGINEERING_SHA} 为准；本轮只修改出版材料。旧作者来源、一致快照、复核退回与查询性能对应这一实现，原始工程材料保存在 docs/round-3。", styles["body"]),
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
        Paragraph("全书分为四部分：风险业务模型；.NET 后端架构与数据一致性；React 调查工作区；测试、故障恢复和工程取舍。每章先独立作答，再阅读参考解；开放题的答案说明本项目选择的依据，不声称只有一种通用设计。", styles["body"]),
        Spacer(1, 12),
        Paragraph("三条阅读路线", styles["frontSub"]),
        Paragraph("业务路线：1–8 → 22 章。沿同一案件识别告警、调查事实、评估、复核和处置；能够回答谁为哪一份判断负责。", styles["body"]),
        Paragraph("后端路线：9–18 → 23–24 章。按一次写请求追到数据库提交，再反向预测并发失败和未知结果。重点比较业务版本、MVCC 快照和幂等回执的职责。", styles["body"]),
        Paragraph("前端路线：19–22 → 16、18 章。先识别服务器状态、草稿和待恢复意图，再解释 ABA、旧响应和账号切换时各自如何被隔离。", styles["body"]),
        Spacer(1, 10),
        Paragraph("贯穿案例与读图约定", styles["frontSub"]),
        Paragraph("Aroha 与 Mateo 的合成交易都出现 South Ridge Services。这个共同邻居是一条核查线索；它不证明共同控制。图中版本数字用于机制推演，真实运行截图与其版本另有说明。深蓝表示已知事实，青色表示接受或当前状态，紫色表示复核/引用，琥珀色提示条件，红色提示拒绝或风险。", styles["body"]),
        Spacer(1, 8),
        Callout(Paragraph("源码定位方法：打开节选标题中显示的仓库相对路径与行号。Markdown 章节和 SVG 原理图均可编辑，PDF 可从仓库工具重建。", styles["body"]), FRAME_W),
        PageBreak(),
        Paragraph("内容目录", styles["frontTitle"]),
    ])
    for index, chapter in enumerate(chapters[:12]):
        story.append(TocRow(f"chapter-{chapter['number']:02d}", f"{chapter['number']:02d}　{chapter['title']}"))
    story.append(PageBreak())
    story.append(Paragraph("内容目录　·　续", styles["frontTitle"]))
    for index, chapter in enumerate(chapters[12:], start=12):
        story.append(TocRow(f"chapter-{chapter['number']:02d}", f"{chapter['number']:02d}　{chapter['title']}"))
    story.extend([Spacer(1, 12), TocRow("appendix-glossary", "附录 A　关键术语"), TocRow("appendix-source-map", "附录 B　源码与章节索引")])
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
    ("MVCC", "PostgreSQL 多版本并发控制。RC 每条语句重新确定可见性；RR 在第一次数据语句固定事务快照，不能与业务 Version 混为一谈。"),
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
    table = Table(data, colWidths=[140, FRAME_W - 140], repeatRows=1, hAlign="LEFT")
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
                directory, filename = excerpt.path.rsplit("/", 1)
                url = f"https://github.com/miku-wwl/riskcase/blob/{ENGINEERING_SHA}/{excerpt.path}#L{excerpt.start}"
                rows.append([Paragraph(f"{chapter['number']:02d}　{markdown_inline(chapter['title'])}", styles["tableCell"]), Paragraph(f'<link href="{url}" color="#117c78"><font name="{CODE_FONT}" size="7.8">{html.escape(directory)}/<br/>{html.escape(filename)} · L{excerpt.start}–{excerpt.end}</font></link>', styles["tableCell"])
                ])
    ref_table = Table(rows, colWidths=[140, FRAME_W - 140], repeatRows=1, hAlign="LEFT")
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
    for chapter in chapters:
        sequence = 0
        for section in chapter["sections"]:
            for match in re.finditer(r"\[(?:figure|screenshot): ([^\]]+)\]", section["body"]):
                name = match.group(1)
                if name not in FIGURE_NUMBERS:
                    sequence += 1
                    FIGURE_NUMBERS[name] = f"{chapter['number']:02d}-{sequence}"
    if args.validate_only:
        figure_count = len({name for chapter in chapters for section in chapter["sections"] for name in FIGURE_RE.findall(section["body"])})
        print(f"Validated {len(chapters)} chapters, {figure_count} SVG references, {len(excerpts)} source excerpts.")
        if missing_screenshots:
            print("Awaiting authentic screenshots: " + ", ".join(missing_screenshots))
        return

    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="riskcase-book-") as temp_name:
        temp_dir = Path(temp_name)
        styles = build_styles()
        # First pass discovers actual chapter/section destinations; second prints them.
        for _ in range(2):
            story = front_matter(chapters, styles)
            story.extend(chapter_flowables(chapters, styles, temp_dir))
            story.extend(appendix_flowables(chapters, excerpts, styles))
            doc = RiskCaseDocTemplate(str(OUTPUT))
            doc.build(story, canvasmaker=functools.partial(pdfcanvas.Canvas, initialFontName=CN_FONT))
    (REFERENCES / "publication-page-map.json").write_text(json.dumps({"engineering_sha": ENGINEERING_SHA, "destinations": PAGE_MAP, "figures": FIGURE_NUMBERS}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (REFERENCES / "publication-layout-decisions.json").write_text(json.dumps(LAYOUT_DECISIONS, indent=2) + "\n", encoding="utf-8")
    print(f"Published {OUTPUT}")


if __name__ == "__main__":
    main()
