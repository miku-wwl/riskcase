# RiskCase 技术教材

当前为 Round 4 图解精修版：93 页 A4、24 章，固定工程基准为 `2177004a39a534ee1796b8391a590763cce62fc2`。应用、测试、迁移与 CI 未修改。正文保存在 `chapters/`；27 张实际引用的可编辑 SVG 由 `tools/build_figures.py` 按基础、Round 2、Round 3、Round 4 顺序生成，本轮重画其中 19 张。

书中 9 张真实界面局部来自 `figures/round4/`，12 组捕获及全屏原图均保留。`capture-manifest.json` 记录真实运行时间、URL、版本和哈希。历史 `riskcase-*.png` 截图仍保留，但不是本版截图来源。

## 构建

需要 Python 3.11+、ReportLab、CairoSVG、Pillow、pypdf、Microsoft YaHei / Consolas 字体和 Poppler 的 `pdftoppm` / `pdfinfo`。结构及图形检查另用 pdfplumber；高分辨率复核可用 PyMuPDF。

```powershell
python manual/tools/build_figures.py
python manual/tools/build_textbook.py --validate-only
python manual/tools/build_textbook.py
python manual/tools/audit_publication_sources.py
python manual/tools/check_figure_labels.py
python manual/tools/inspect_pdf.py manual/dist/RiskCase-Financial-Risk-Management-Engineering-Textbook.pdf --json manual/reference/round4-final-pdf-structure.json --page-map manual/reference/publication-page-map.json
```

构建器验证唯一源码锚点和完整节选范围，将 31 段实际源码行号写入 `reference/source-map.md`。两遍排版产生真实目录/书签目标页与 `publication-page-map.json`；页数不按固定模板推算。相同输入在本轮环境连续构建得到相同字节；PDF 元数据使用 ReportLab 的固定时间原点，实际出版日期见审查报告。

最终出版前仍须渲染并实际逐页检查。`reference/final-independent-publication-review.md` 是本版审查报告；两份 page-review JSON 分别覆盖原版 127 页与新版 93 页，联系表仅作补充，不代替单页审看。旧版 QA 文件按其标题保留为历史记录。

PDF 使用的是真实运行中的界面截图；不要以示意图或图像生成替换这些截图。源 SVG 与各章 Markdown 是主要可编辑出版材料。

本轮捕获工具为 `tools/capture_publication_ui.mjs`，复用现有 Playwright 安装，要求真实本地服务和独立合成数据库。它会通过 API 创建新出版案例，不能针对真实业务数据运行。`tools/verify_publication_capture.py` 使用独立 psql 只读事务核对截图案例的提交事实。运行观察和未扩大修复范围的展示问题见 `reference/round4-source-grounding.md`。
