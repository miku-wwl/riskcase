"""Verify every printed excerpt against the pinned engineering commit."""
import hashlib
import json
import re
import subprocess
from pathlib import Path
from build_textbook import ROOT, ENGINEERING_SHA, SOURCE_RE, source_excerpt

entries = []
files = {}
chapter_sizes = []
for path in sorted((ROOT / "manual/chapters").glob("[0-9][0-9]-*.md")):
    text = path.read_text(encoding="utf-8")
    original = subprocess.check_output(["git", "show", ENGINEERING_SHA+":"+path.relative_to(ROOT).as_posix()]).decode("utf-8")
    clean = lambda value: re.sub(r"\[(?:source|figure|screenshot):[^\]]+\]|<!--.*?-->", "", value)
    chapter_sizes.append({"chapter": int(path.name[:2]), "original_teaching_characters": len(clean(original)), "revised_teaching_characters": len(clean(text))})
    for name, anchor, count in SOURCE_RE.findall(text):
        excerpt = source_excerpt(name, anchor, int(count))
        actual = (ROOT / name).read_bytes()
        pinned = subprocess.check_output(["git", "show", ENGINEERING_SHA+":"+name])
        assert actual.replace(b"\r\n", b"\n") == pinned.replace(b"\r\n", b"\n"), name
        files[name] = hashlib.sha256(actual.replace(b"\r\n", b"\n")).hexdigest()
        entries.append({"chapter": int(path.name[:2]), "path":name, "start":excerpt.start, "end":excerpt.end, "anchor":anchor, "excerpt_sha256":hashlib.sha256("\n".join(excerpt.lines).encode()).hexdigest()})
protected = subprocess.check_output(["git", "diff", "--name-only", ENGINEERING_SHA, "--", "backend", "frontend", "test", ".github/workflows"]).decode().splitlines()
assert not protected, protected
result = {"engineering_sha":ENGINEERING_SHA,"protected_paths_changed":protected,"source_files":files,"excerpts":entries,"chapters":chapter_sizes}
(ROOT / "manual/reference/round4-source-alignment.json").write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
print(f"Verified {len(entries)} actual excerpts in {len(files)} unchanged source files against the engineering baseline.")
