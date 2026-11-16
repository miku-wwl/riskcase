# RiskCase 技术教材

教材正文以 24 个可编辑章节保存于 `chapters/`，24 张可编辑 SVG 原理图由 `figures/diagrams.json` 和 `tools/build_figures.py` 生成。真实界面截图由 Playwright 在 PostgreSQL 合成样例上采集，文件名为 `figures/riskcase-*.png`。

## 构建

需要 Python 3.11+、ReportLab、CairoSVG、Pillow、pypdf、Microsoft YaHei 字体和 Poppler 的 `pdftoppm` / `pdfinfo`。

```powershell
python manual/tools/build_figures.py
python manual/tools/build_textbook.py --validate-only
python manual/tools/build_textbook.py
```

构建器会验证章节、原理图和源码引用，将实际引用的源码行号写入 `reference/source-map.md`，并生成 `dist/RiskCase-Financial-Risk-Management-Engineering-Textbook.pdf`。最终出版前需用 Poppler 渲染全部页面并检查版式；`reference/publication-qa.md` 记录最后一次实际审阅证据。

PDF 使用的是真实运行中的界面截图；不要以示意图或图像生成替换这些截图。源 SVG 与各章 Markdown 是主要可编辑出版材料。
