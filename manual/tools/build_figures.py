from __future__ import annotations

import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FIGURES = ROOT / "manual" / "figures"
PALETTE = {
    "navy": ("#eef3f9", "#22364f", "#4d6074"),
    "teal": ("#e8f5f3", "#117c78", "#466b68"),
    "amber": ("#fff4df", "#b87414", "#765e39"),
    "coral": ("#fff0ed", "#bd5448", "#77524e"),
    "purple": ("#f1edfb", "#7255a5", "#625576"),
}


def label(text: str, x: float, y: float, size: int, color: str, weight: int = 400) -> str:
    return f'<text x="{x}" y="{y}" font-family="Microsoft YaHei, Noto Sans CJK SC, sans-serif" font-size="{size}px" font-weight="{weight}" fill="{color}">{html.escape(text)}</text>'


def render(figure: dict) -> str:
    width, height = 900, 460
    nodes = {node["id"]: node for node in figure["nodes"]}
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}">',
        '<rect width="100%" height="100%" rx="18" fill="#ffffff"/>',
        '<path d="M 30 77 H 870" stroke="#e6edf2" stroke-width="1"/>',
        label(figure["title"], 38, 39, 21, "#22364f", 700),
        label(figure["subtitle"], 38, 63, 12, "#64748b"),
        '<defs><marker id="arrow" viewBox="0 0 10 10" refX="8.5" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="#718096"/></marker></defs>',
    ]
    for edge in figure["edges"]:
        source, target = nodes[edge["from"]], nodes[edge["to"]]
        sw, sh = source.get("w", 182), source.get("h", 112)
        tw, th = target.get("w", 182), target.get("h", 112)
        sx0, sy0 = source["x"] + sw / 2, source["y"] + sh / 2
        tx0, ty0 = target["x"] + tw / 2, target["y"] + th / 2
        dx, dy = tx0 - sx0, ty0 - sy0
        curve = edge.get("curve", 0)
        if abs(dx) > abs(dy):
            if dx >= 0:
                sx, tx = source["x"] + sw, target["x"]
                sy, ty = sy0, ty0
                if curve:
                    c = curve
                    d = f"M {sx} {sy} C {sx + 60} {sy - c}, {tx - 60} {ty - c}, {tx} {ty}"
                    lx, ly = (sx + tx) / 2, (sy + ty) / 2 - c + 3
                else:
                    d = f"M {sx} {sy} C {sx + 42} {sy}, {tx - 42} {ty}, {tx} {ty}"
                    lx, ly = (sx + tx) / 2, (sy + ty) / 2 - 8
            else:
                sx, tx = source["x"], target["x"] + tw
                sy, ty = sy0, ty0
                c = curve or 48
                d = f"M {sx} {sy} C {sx - 35} {sy - c}, {tx + 35} {ty - c}, {tx} {ty}"
                lx, ly = (sx + tx) / 2, (sy + ty) / 2 - c + 4
        else:
            if dy >= 0:
                sx, tx = sx0, tx0
                sy, ty = source["y"] + sh, target["y"]
                d = f"M {sx} {sy} C {sx} {sy + 42}, {tx} {ty - 42}, {tx} {ty}"
                lx, ly = (sx + tx) / 2 + 8, (sy + ty) / 2
            else:
                sx, tx = sx0, tx0
                sy, ty = source["y"], target["y"] + th
                c = curve or 45
                d = f"M {sx} {sy} C {sx - c} {sy - 35}, {tx - c} {ty + 35}, {tx} {ty}"
                lx, ly = (sx + tx) / 2 - c, (sy + ty) / 2
        parts.append(f'<path d="{d}" fill="none" stroke="#718096" stroke-width="2" marker-end="url(#arrow)"/>')
        if edge.get("label"):
            text = html.escape(edge["label"])
            parts.append(f'<rect x="{lx - 37}" y="{ly - 14}" width="74" height="19" rx="8" fill="#ffffff" stroke="#e2e8f0"/>')
            parts.append(f'<text x="{lx}" y="{ly}" text-anchor="middle" font-family="Microsoft YaHei, Noto Sans CJK SC, sans-serif" font-size="10px" fill="#526273">{text}</text>')
    for node in figure["nodes"]:
        w, h = node.get("w", 182), node.get("h", 112)
        fill, accent, body = PALETTE[node.get("tone", "navy")]
        x, y = node["x"], node["y"]
        parts.extend([
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="13" fill="{fill}" stroke="{accent}" stroke-width="1.4"/>',
            f'<path d="M {x + 14} {y + 14} V {y + h - 14}" stroke="{accent}" stroke-width="3" stroke-linecap="round"/>',
        ])
        title_lines = node["title"].split("|")
        for index, line in enumerate(title_lines):
            parts.append(label(line, x + 27, y + 34 + index * 19, 14, "#22364f", 700))
        base = y + 66 + (len(title_lines) - 1) * 18
        for index, line in enumerate(node.get("detail", "").split("|")):
            parts.append(label(line, x + 27, base + index * 17, 11, body))
    parts.extend([
        '<path d="M 30 414 H 870" stroke="#e6edf2" stroke-width="1"/>',
        label(figure.get("footer", ""), 38, 439, 11, "#64748b"),
        "</svg>",
    ])
    return "\n".join(parts)


def main() -> None:
    config = json.loads((FIGURES / "diagrams.json").read_text(encoding="utf-8"))
    for figure in config["figures"]:
        path = FIGURES / figure["file"]
        path.write_text(render(figure), encoding="utf-8", newline="\n")
    from build_round2_figures import build
    build()
    from build_round3_figures import build as build_round3
    build_round3()
    print(f"Generated {len(config['figures'])} editable SVG diagrams.")


if __name__ == "__main__":
    main()
