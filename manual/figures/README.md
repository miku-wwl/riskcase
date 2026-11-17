# RiskCase figures

Vector diagrams used by the technical chapters remain editable SVG files. The Round 4 book uses 27 distinct mechanisms; 19 were redrawn by `../tools/build_round4_figures.py`, invoked after the earlier figure generators by `build_figures.py`. Captions are stored in `publication-captions.json`.

The current book uses nine native interface regions from `round4/`. Twelve captured regions and their full-screen originals in `round4/raw/` are retained with timestamps, URLs, versions and SHA-256 values in `round4/capture-manifest.json`. They come from the actual API, Vite, Chromium and a separate PostgreSQL database with synthetic data. They are not mockups, generated images, or redrawn interfaces. Historical `riskcase-*.png` captures remain available but are not the current edition's source.
