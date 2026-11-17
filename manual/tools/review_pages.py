"""Record actual individual-page publication inspections outside the reader-facing book."""
import argparse
import hashlib
import json
import sys
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--ledger", required=True, type=Path)
    parser.add_argument("--pdf", required=True, type=Path)
    parser.add_argument("--pages", required=True, help="Comma-separated page numbers actually viewed")
    parser.add_argument("--notes", required=True, help="JSON object keyed by page number")
    args = parser.parse_args()
    notes = json.loads(sys.stdin.read() if args.notes == "-" else args.notes)
    digest = hashlib.sha256(args.pdf.read_bytes()).hexdigest()
    data = json.loads(args.ledger.read_text(encoding="utf-8")) if args.ledger.exists() and args.ledger.stat().st_size else {"pdf_sha256": digest, "pages": {}}
    if data["pdf_sha256"] != digest:
        raise ValueError("The PDF changed: start a new inspection ledger.")
    for number in args.pages.split(","):
        number = str(int(number))
        if number not in notes:
            raise ValueError(f"Missing explicit observation for page {number}")
        data["pages"][number] = {"method": "individual rendered page", "observation": notes[number]}
    args.ledger.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.ledger.with_suffix(".tmp")
    temporary.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(args.ledger)
    print(f"Recorded {len(data['pages'])} individually viewed pages.")


if __name__ == "__main__":
    main()
