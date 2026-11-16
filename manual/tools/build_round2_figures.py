"""Editable source for the mechanisms changed by Round 2; called by build_figures.py."""
import html
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "figures"
COLORS = {"navy": ("#eef3f9", "#22364f"), "teal": ("#e8f5f3", "#117c78"), "amber": ("#fff4df", "#a36c13"), "coral": ("#fff0ed", "#b84940"), "purple": ("#f1edfb", "#7255a5")}

class Diagram:
    def __init__(self, title, subtitle, footer):
        self.parts = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 900 500" width="900" height="500">', '<rect width="900" height="500" fill="white"/>', '<defs><marker id="a" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0 0L8 4L0 8" fill="none" stroke="#64748b"/></marker></defs>']
        self.text(30, 36, title, 23, "#22364f", True); self.text(30, 63, subtitle, 14)
        self.parts.append('<path d="M30 79H870 M30 457H870" stroke="#dbe5ea"/>')
        self.text(30, 482, footer, 13)
    def text(self, x, y, value, size=15, color="#526273", bold=False):
        self.parts.append(f'<text x="{x}" y="{y}" font-family="Microsoft YaHei,Noto Sans CJK SC,sans-serif" font-size="{size}" fill="{color}" font-weight="{700 if bold else 400}">{html.escape(value)}</text>')
    def box(self, x, y, w, h, title, lines=(), tone="teal"):
        fill, stroke = COLORS[tone]; self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>')
        self.text(x+15, y+28, title, 17, stroke, True)
        for i, value in enumerate(lines): self.text(x+15, y+54+i*22, value, 14)
    def arrow(self, x1,y1,x2,y2, label=""):
        self.parts.append(f'<path d="M{x1} {y1}L{x2} {y2}" stroke="#64748b" stroke-width="1.6" fill="none" marker-end="url(#a)"/>')
        if label: self.text((x1+x2)/2-30,(y1+y2)/2-9,label,12)
    def lane(self, y, label):
        self.text(30,y+5,label,16,"#22364f",True); self.parts.append(f'<path d="M165 {y}H865" stroke="#dbe5ea" stroke-dasharray="5 4"/>')
    def write(self,name): (ROOT/name).write_text("\n".join(self.parts+["</svg>"]),encoding="utf-8")

def build():
    d=Diagram("状态同时约束事实、评估与责任人", "复核是一份已确认调查基础的冻结期；显式退回后必须重新评估", "Resolved 终止本系统调查；监测、升级、AML 转交属于下游人工流程")
    for x,title,lines,tone in [(30,"New",["分派 → 开始调查"],"navy"),(245,"Investigating",["笔记 / 关系可追加","变更使当前评估失效"],"teal"),(465,"Reviewing",["事实 / 评估 / 分派冻结","绑定评估 ID"],"purple"),(695,"Resolved",["决定与历史只读"],"navy")]: d.box(x,130,180,118,title,lines,tone)
    d.arrow(210,180,245,180);d.arrow(425,180,465,180);d.arrow(645,180,695,180)
    d.arrow(550,248,550,320); d.arrow(550,320,335,320); d.arrow(335,320,335,248)
    d.text(365,346,"经办人或主管显式退回",15);d.text(65,410,"新评估确认当前基础 → 再次提交复核 → 另一位合格主管决定",18,"#117c78",True)
    d.write("05-case-lifecycle.svg")

    d=Diagram("复核引用评估身份，不依赖当前分派推测作者", "评估历史只追加；转派不能抹去实际作者的责任", "本案任何历史评估作者均不能作最终处置；所有风险级别均要求独立主管复核")
    d.box(35,125,235,120,"评估 A1",["作者：S · High","原理由与版本永久保留"],"amber")
    d.box(330,125,235,120,"评估 A2（当前）",["作者：A · Low","调查基础 v8 · 评估 v9"],"teal")
    d.box(625,125,235,120,"复核提交",["ReviewedAssessmentId=A2","提交人 A · 提交版本 v10"],"purple")
    d.arrow(270,182,330,182,"新记录");d.arrow(565,182,625,182,"引用")
    d.box(330,305,235,110,"主管 R 的决定",["AssessmentId=A2","决定 / 理由 / 版本"],"navy")
    d.arrow(740,245,740,355);d.arrow(740,355,565,355,"对应")
    d.text(40,340,"S 与 A 均不可成为 R",17,"#b84940",True);d.text(40,372,"重新分派、退回、重评",15);d.text(40,398,"不会消除历史作者身份",15)
    d.write("08-assessment-review-gate.svg")

    d=Diagram("聚合内存与数据库事实必须对齐", "导航集合未加载不等于数据库中没有记录", "Include(Links, Assessments) 支撑领域判断；唯一约束和并发版本构成最后边界")
    d.box(35,120,375,145,"读取：加载规则需要的历史",["RiskCase：Unchanged · 原始 Version=v","Links：已持久化端点，检测重复关系","Assessments：所有已知评估作者"],"navy")
    d.box(485,120,375,145,"操作：显式声明子记录新增",["领域方法返回 RiskAssessment / Note","DbSet.Add(child) → Added","案件字段变化 → Modified"],"teal")
    d.arrow(410,190,485,190,"执行")
    d.box(35,315,825,105,"同一事务写入",["INSERT 子记录 + INSERT 审计 + UPDATE 案件 WHERE Version=v","任一失败 → 回滚全部变化；成功后仍须随幂等回执一起 COMMIT"],"amber")
    d.arrow(670,265,670,315)
    d.write("14-ef-change-tracking.svg")

    d=Diagram("MVCC 可见性与业务版本条件各有职责", "Read Committed 下的两个请求：读取同一 v8，再竞争写入", "图示为 RiskCase 的条件更新；不是 PostgreSQL 对所有 UPDATE 的默认防覆盖保证")
    d.lane(160,"事务 T1"); d.lane(295,"事务 T2")
    d.box(180,113,175,93,"读到 v8",["原始并发令牌"],"navy");d.box(430,113,175,93,"写入 v9",["WHERE Version=8"],"teal");d.box(680,113,170,93,"COMMIT",["审计与回执一起"],"teal")
    d.box(180,248,175,93,"读到 v8",["另一个请求快照"],"navy");d.box(610,248,240,93,"旧条件更新 0 行",["WHERE Version=8 → 412"],"coral")
    d.arrow(355,160,430,160);d.arrow(605,160,680,160);d.arrow(355,295,610,295);d.arrow(765,206,765,248)
    d.text(180,407,"EF 可能先插入子记录；若版本冲突，外层事务仍回滚这些 INSERT。",17)
    d.write("15-postgresql-mvcc.svg")

    d=Diagram("历史成功可以恢复，当前状态必须重新确认", "回执回答“这次操作当时是否成功”，不承诺“这是现在的案件状态”", "实时角色校验 → 原回执 vN → 缓存取较新版本 → GET 当前状态；不再次执行业务写入")
    for y,label in [(135,"浏览器"),(235,"服务端"),(335,"PostgreSQL")]:d.lane(y,label)
    d.box(180,99,200,76,"原意图 K / vN−1",["内容和前置条件固定"],"navy")
    d.box(430,199,180,76,"响应丢失",["客户端结果未知"],"coral")
    d.box(180,299,200,105,"一次 COMMIT",["操作成功 → vN","业务 + 审计 + 回执 K"],"teal")
    d.box(650,99,215,105,"恢复 K 后再核对",["旧快照不能覆盖 vN+1","刷新得到当前确认状态"],"purple")
    d.box(650,299,215,105,"后来已到 vN+1",["K 仍保存历史成功 vN","不重复插入业务记录"],"amber")
    d.arrow(280,175,280,299);d.arrow(380,335,430,275);d.arrow(610,235,650,204);d.arrow(755,299,755,204)
    d.write("16-concurrency-and-idempotency.svg")

    d=Diagram("用三个不同版本解释 A → B → A", "文本相等不代表编辑意图相同；r1、r2、r3 为编辑代次示意", "只有案件键、用户键、本地代次与跨标签页持久化代次均匹配时，成功回调才清空草稿")
    d.lane(155,"本地编辑");d.lane(300,"网络请求")
    for x,title,lines,tone in [(180,"A / r1",["提交时捕获快照"],"navy"),(420,"B / r2",["用户继续修改"],"amber"),(660,"A / r3",["内容回到 A，代次已变"],"teal")]:d.box(x,108,205,98,title,lines,tone)
    d.arrow(385,155,420,155);d.arrow(625,155,660,155)
    d.box(180,253,205,100,"提交 M1",["内容 A · 捕获 r1","固定案件与服务端版本"],"navy")
    d.box(660,253,205,100,"M1 成功返回",["r1 ≠ 当前 r3","保留当前草稿 A"],"purple")
    d.arrow(280,206,280,253);d.arrow(385,300,660,300,"延迟响应")
    d.text(180,414,"案件缓存另用服务器 Version 比较；草稿代次和服务器版本不能互相替代。",16)
    d.write("20-react-state-boundaries.svg")

if __name__ == "__main__": build()
