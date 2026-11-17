"""Check original SVG text extents with the publication fonts; does not replace visual QA."""
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from PIL import ImageFont

root = Path(__file__).resolve().parents[1]
issues, checked = [], 0
for path in sorted((root / "figures").glob("*.svg")):
    svg = ET.parse(path).getroot()
    width, height = map(float, svg.attrib.get("viewBox", "0 0 900 500").split()[2:])
    rects = [e for e in svg.iter() if e.tag.endswith("}rect") and all(k in e.attrib for k in ("x", "y", "width", "height"))]
    for e in svg.iter():
        if not e.tag.endswith("}text"): continue
        value = "".join(e.itertext())
        x, y = float(e.attrib.get("x", 0)), float(e.attrib.get("y", 0))
        size = float(e.attrib.get("font-size", "16").rstrip("px"))
        face = "msyhbd.ttc" if e.attrib.get("font-weight") == "700" else "msyh.ttc"
        extent = ImageFont.truetype("C:/Windows/Fonts/"+face, round(size*10)).getlength(value)/10
        left = x-extent/2 if e.attrib.get("text-anchor") == "middle" else x
        right, problem = left+extent, None
        checked += 1
        if left < -1 or right > width+1 or y > height: problem = "outside SVG"
        for r in rects:
            rx, ry, rw, rh = [float(r.attrib[k]) for k in ("x", "y", "width", "height")]
            if abs(x-rx-14)<.1 and ry<y<ry+rh and right>rx+rw-5: problem = "exceeds owning box"
        if problem: issues.append({"file":path.name,"text":value,"problem":problem,"right":round(right,2)})
(root/"reference/figure-label-check.json").write_text(json.dumps({"checked_labels":checked,"issues":issues},ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(f"Checked {checked} SVG labels; {len(issues)} extent warnings.")
for item in issues: print(item["file"],item["problem"],item["text"].encode("unicode_escape").decode())
if issues: raise SystemExit(1)
