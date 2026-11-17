"""Inspect publication structure and render contact sheets; visual review remains manual."""
import argparse
import hashlib
import json
from pathlib import Path

import pdfplumber
from PIL import Image, ImageDraw
from pypdf import PdfReader


def inspect(pdf):
    reader = PdfReader(pdf)
    page_numbers = {(page.indirect_reference.idnum, page.indirect_reference.generation): number
                    for number, page in enumerate(reader.pages, 1)}
    outlines = []
    def walk(items):
        for item in items:
            if isinstance(item, list):
                walk(item)
            else:
                outlines.append({"title": item.title, "page": reader.get_destination_page_number(item) + 1})
    walk(reader.outline)
    pages, fonts, outside = [], {}, []
    with pdfplumber.open(pdf) as document:
        for number, (page, raw) in enumerate(zip(document.pages, reader.pages), 1):
            chars = page.chars
            for char in chars:
                if char["x0"] < -0.5 or char["x1"] > page.width + 0.5 or char["top"] < -0.5 or char["bottom"] > page.height + 0.5:
                    outside.append({"page": number, "text": char["text"], "box": [char["x0"], char["top"], char["x1"], char["bottom"]]})
            links = []
            for annotation in raw.get("/Annots", []):
                value = annotation.get_object()
                if value.get("/Subtype") == "/Link":
                    action = value.get("/A", {})
                    destination = value.get("/Dest")
                    target = None
                    if destination and isinstance(destination, list):
                        reference = destination[0]
                        target = page_numbers.get((reference.idnum, reference.generation))
                    links.append({"destination": str(destination or ""), "destination_page": target,
                                  "uri": str(action.get("/URI", ""))})
            for reference in raw.get("/Resources", {}).get("/Font", {}).values():
                font = reference.get_object()
                name = str(font.get("/BaseFont"))
                descriptor = font.get("/FontDescriptor")
                if descriptor:
                    descriptor = descriptor.get_object()
                used = any(name.split("+")[-1] in char["fontname"] for char in chars)
                fonts[name] = {"embedded": bool(descriptor and any(key in descriptor for key in ("/FontFile", "/FontFile2", "/FontFile3"))), "used": used or fonts.get(name, {}).get("used", False)}
            pages.append({"page": number, "size": [float(page.width), float(page.height)], "characters": len(chars), "images": len(page.images), "links": links})
    return {"pdf": str(pdf), "sha256": hashlib.sha256(pdf.read_bytes()).hexdigest(), "bytes": pdf.stat().st_size,
            "metadata": dict(reader.metadata), "page_count": len(pages), "bookmarks": outlines,
            "fonts": fonts, "out_of_page_characters": outside, "pages": pages}


def check_navigation(result, page_map):
    expected = [value for key, value in page_map["destinations"].items()
                if key.startswith("chapter-") or key.startswith("appendix-")]
    targets = [link["destination_page"] for page in result["pages"] for link in page["links"] if link["destination"]]
    checks = {
        "all_pages_a4": all(abs(page["size"][0] - 595.2756) < 0.1 and abs(page["size"][1] - 841.8898) < 0.1 for page in result["pages"]),
        "all_used_fonts_embedded": all(font["embedded"] for font in result["fonts"].values() if font["used"]),
        "bookmarks_match_actual_destinations": [item["page"] for item in result["bookmarks"]] == expected,
        "toc_links_match_actual_destinations": targets == expected,
        "no_characters_outside_page": not result["out_of_page_characters"],
    }
    result["navigation_check"] = {"expected_destinations": expected, "internal_link_destinations": targets,
                                  "checks": checks, "passed": all(checks.values())}
    if not all(checks.values()):
        raise ValueError("Publication structural checks failed: " + str(checks))


def contact_sheets(directory, output):
    paths = sorted(directory.glob("page-*.png"))
    output.mkdir(parents=True, exist_ok=True)
    for start in range(0, len(paths), 16):
        sheet = Image.new("RGB", (4 * 250, 4 * 370), "#e6edf2")
        draw = ImageDraw.Draw(sheet)
        for index, path in enumerate(paths[start:start + 16]):
            with Image.open(path) as source:
                source.thumbnail((234, 338))
                x, y = (index % 4) * 250 + 8, (index // 4) * 370 + 25
                sheet.paste(source, (x, y))
                draw.text((x, y - 19), path.stem, fill="#22364f")
        sheet.save(output / f"contact-{start + 1:03d}.jpg", quality=90)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("pdf", type=Path)
    parser.add_argument("--json", type=Path, required=True)
    parser.add_argument("--rendered", type=Path)
    parser.add_argument("--contacts", type=Path)
    parser.add_argument("--page-map", type=Path, help="Compare bookmarks and TOC links with actual build destinations.")
    args = parser.parse_args()
    result = inspect(args.pdf)
    if args.page_map:
        check_navigation(result, json.loads(args.page_map.read_text(encoding="utf-8")))
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if args.rendered and args.contacts:
        contact_sheets(args.rendered, args.contacts)
    print(f"Inspected {result['page_count']} pages; {len(result['bookmarks'])} bookmarks; {len(result['out_of_page_characters'])} characters outside page.")
