"""Source-grounded publication diagrams. Labels express ownership, time or actual control flow."""
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "figures"
PALETTE = {"navy": ("#eef3f9", "#22364f"), "teal": ("#e8f5f3", "#117c78"), "amber": ("#fff4df", "#a36c13"), "coral": ("#fff0ed", "#b84940"), "purple": ("#f1edfb", "#7255a5")}
CAPTIONS = {}


class Sheet:
    def __init__(self, title, subtitle, footer, height=440):
        self.height = height
        self.parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="900" height="{height}" viewBox="0 0 900 {height}">', '<rect width="900" height="100%" fill="white"/>', '<defs><marker id="arrow" markerWidth="8" markerHeight="8" refX="7" refY="4" orient="auto"><path d="M0 0L8 4L0 8" fill="none" stroke="#64748b"/></marker></defs>']
        self.text(24, 32, title, 24, "#22364f", True)
        self.text(24, 61, subtitle, 16)
        self.path("M24 79H876", "#dbe5ea", marker=False)
        self.path(f"M24 {height-42}H876", "#dbe5ea", marker=False)
        self.text(24, height-15, footer, 15)

    def text(self, x, y, value, size=17, color="#526273", bold=False):
        self.parts.append(f'<text x="{x}" y="{y}" font-family="Microsoft YaHei,Noto Sans CJK SC,sans-serif" font-size="{size}" fill="{color}" font-weight="{700 if bold else 400}">{html.escape(value)}</text>')

    def box(self, x, y, w, h, title, lines=(), tone="teal"):
        fill, accent = PALETTE[tone]
        self.parts.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="9" fill="{fill}" stroke="{accent}" stroke-width="1.5"/>')
        self.text(x+14, y+28, title, 19, accent, True)
        for i, line in enumerate(lines):
            self.text(x+14, y+54+i*24, line, 17)

    def path(self, value, color="#64748b", marker=True, dash=False):
        self.parts.append(f'<path d="{value}" fill="none" stroke="{color}" stroke-width="1.7"'+(' marker-end="url(#arrow)"' if marker else '')+(' stroke-dasharray="5 5"' if dash else '')+'/>')

    def arrow(self, x1, y1, x2, y2):
        self.path(f"M{x1} {y1}L{x2} {y2}")

    def write(self, name, caption):
        (ROOT / name).write_text("\n".join(self.parts+["</svg>"]), encoding="utf-8", newline="\n")
        CAPTIONS[name] = caption


def build():
    d = Sheet("一条告警跨越三种责任", "交接的是观察和判断；处置记录有明确的系统边界", "87 分用于安排核查顺序；来源事实、人员判断与外部行动分别负责")
    d.box(25,115,250,155,"上游监测",["输入：交易观察","输出：有来源的告警","信号 + 引用 + 可选分数"],"navy")
    d.box(325,115,250,155,"RiskCase 调查",["经办人：事实与评估","独立主管：复核与决定","保存作者、依据及版本"],"teal")
    d.box(625,115,250,155,"下游人工团队",["读取升级 / 监测建议","是否接收、怎样行动","本应用没有执行回执"],"purple")
    d.arrow(275,190,325,190); d.arrow(575,190,625,190)
    d.box(125,308,650,65,"本系统没有付款、冻结账户或余额写入接口",[],"amber")
    d.write("01-system-boundary.svg", "沿输入与输出区分责任：Created 表示案件接收成功；Resolved 只结束本系统调查。")

    d = Sheet("两把锁解决两种不同的重复", "API 密钥 → 输入验证；随后才进入下面的事务边界", "所有成功路径最后保存各自回执并 COMMIT；拒绝路径不提交部分业务记录", 520)
    d.parts.append('<rect x="20" y="96" width="860" height="375" rx="12" fill="#fafcfd" stroke="#94a3b8" stroke-dasharray="6 5"/>')
    d.box(40,115,525,85,"① 开始事务 → scope/key 锁 → 查原回执",["已有同指纹：返回历史结果，不运行 Handler"],"purple")
    d.box(620,115,240,85,"原回执存在",["内容变化 → 409"],"amber")
    d.arrow(565,156,620,156)
    d.text(45,235,"只有新意图进入 Handler",18,"#117c78",True)
    d.box(40,260,250,140,"② 规范化来源身份",["SourceSystem + ID","取得业务身份锁","同来源即使换键仍协调"],"navy")
    d.box(330,260,235,140,"③ 比较 / 核验",["已有：比规范化载荷","新告警：校验所有权","信号与交易引用有效"],"teal")
    d.box(610,260,250,140,"④ 保存并回读",["新案 + 审计；或已有案","外层新增该键回执","SaveChanges → COMMIT"],"teal")
    d.arrow(290,330,330,330); d.arrow(565,330,610,330)
    d.text(45,440,"来源内容不同：409 external_alert_payload_conflict；不会覆盖原始观察。",17,"#b84940")
    d.write("02-alert-intake.svg", "事务级幂等键锁在 Handler 外；来源身份锁在 Handler 内。传输重复与业务重复共享提交边界。")

    d = Sheet("持久化关系与概念节点不是同一层", "Customer / Account / RiskTransaction 是实体；对手方引用来自交易字段", "实线表示实际外键或值来源；下方说明案件围绕来源告警组织调查")
    for x,title,lines,tone in [(25,"Customer",["稳定客户 ID","不等于告警主体"],"navy"),(245,"Account",["CustomerId 外键","掩码账号，无可写余额"],"teal"),(465,"RiskTransaction",["AccountId 外键","金额 / 时间 / 引用"],"amber"),(685,"对手方节点",["Counterparty","Reference","来自交易中的值"],"purple")]:
        d.box(x,120,190,135,title,lines,tone)
    d.arrow(245,185,215,185); d.arrow(465,185,435,185); d.arrow(655,185,685,185)
    d.box(100,302,700,68,"RiskCase → RiskAlert → 主客户/主账户与相关观察",[],"navy")
    d.write("03-investigation-entities.svg", "外键箭头指向被引用实体。对手方是交易值投影出的节点，当前模型没有独立对手方实体表。")

    d = Sheet("同一事实可以有规则解释，也可以有反证", "核对交易引用后再判断；分数不是每个调查阶段的必经节点", "信号不能证明欺诈或洗钱；本系统没有重新执行一个上游风险规则引擎")
    d.box(25,110,250,140,"观察交易",["TX-1 … TX-4","时间 / 金额 / 对手方","先排除错误及重复来源"],"navy")
    d.box(325,110,250,140,"可解释信号",["代码 + 理由 + 引用","例如：短时多次转出","可被事实反驳"],"amber")
    d.box(625,110,250,140,"人员调查与评估",["结合背景及反证","记录完整理由与作者","再提交独立复核"],"teal")
    d.arrow(275,180,325,180); d.arrow(575,180,625,180)
    d.box(100,300,700,70,"可选分数 87 → 队列优先级；不能直接推出最终处置",[],"purple")
    d.write("04-signal-evidence.svg", "事实、规则解释、排序提示和人员决定各有证据强度；可选分数不会自动成为评估。")

    d = Sheet("队列过滤改变视图，写入资格改变可做的事", "所有 CaseReaders 读取全案；AssignedToMe 仅增加查询条件", "稳定排序：严重度权重降序 → 检测时间升序 → ID；列表分页不等于详情集合分页")
    d.box(25,115,250,145,"数据库查询",["状态 / 类别 / 严重度","搜索 / AssignedToMe","Count 与当前页读取"],"navy")
    d.box(325,115,250,145,"列表视图",["Skip / Take ≤ 100","来源、客户、状态、经办","打开案件时再读详情"],"teal")
    d.box(625,115,250,145,"写入另行检查",["当前角色与目标资格","当前经办人 + 版本","领域状态与独立性"],"purple")
    d.arrow(275,185,325,185)
    d.path("M600 105V278", "#b87414", marker=False, dash=True)
    d.box(100,310,700,65,"界面隐藏按钮可以提示规则；服务器必须拒绝直接越权请求",[],"amber")
    d.write("06-queue-assignment.svg", "左侧回答看哪些案件，右侧回答能执行哪个动作。查询筛选不能承担资源授权。")

    d = Sheet("两条真实来源路径汇合到同一引用", "这是共同邻居线索，不是两名客户之间的直接关系", "图中 TX / 账户代号是教学缩写；共享商户不能证明相识或共同控制", 470)
    for y,customer,account,tx in [(125,"Aroha","账户 A","TX-1"),(290,"Mateo","账户 B","TX-4")]:
        d.box(25,y,170,90,customer,["客户"],"navy")
        d.box(240,y,170,90,account,["实际所属账户"],"teal")
        d.box(455,y,170,90,tx,["独立观察记录"],"amber")
        d.arrow(195,y+45,240,y+45); d.arrow(410,y+45,455,y+45)
    d.text(208,112,"持有",16); d.text(420,112,"活动",16)
    d.box(705,207,170,115,"South Ridge",["Services","CP-845 交易引用"],"purple")
    d.arrow(625,170,705,240); d.arrow(625,335,705,285)
    d.write("07-persisted-relationship-graph.svg", "沿客户、账户、交易回到 CP-845。对手方按稳定引用合并；客户之间没有凭空新增的边。")

    d = Sheet("只画实际直接项目引用", "编译依赖向内；运行时通过接口解析到外层实现", "Domain 不引用其余三个项目；测试项目不被任何生产项目引用")
    d.box(25,215,220,100,"API",["启动与 HTTP 组合根"],"purple")
    d.box(335,110,225,100,"Application",["用例与自身接口"],"teal")
    d.box(335,290,225,100,"Infrastructure",["EF / PostgreSQL","Identity 实现"],"amber")
    d.box(650,110,225,100,"Domain",["规则、不变量与实体"],"navy")
    d.arrow(245,245,335,160); d.arrow(245,275,335,340); d.arrow(445,290,445,210); d.arrow(560,160,650,160)
    d.text(25,125,"箭头 A → B：",17,"#22364f",True); d.text(25,153,"A 的项目引用 B",17)
    d.write("09-clean-architecture-dependencies.svg", "API 引用 Application 和 Infrastructure；Infrastructure 引用 Application；Application 引用 Domain。")

    d = Sheet("一次实际评估写请求的进入顺序", "PUT /api/cases/{id}/assessment；异常处理中间件包住下游执行", "CSRF、当前身份、资源权限、版本和幂等分别保护不同条件", 475)
    for x,title,lines,tone in [(25,"① Routing / CORS",["选择 endpoint metadata","限定跨域来源"],"navy"),(325,"② 身份与端点授权",["Cookie 解析主体","策略按端点角色检查"],"purple"),(625,"③ 绑定 / Filter",["参数与 JSON 正文","写入检查 CSRF token"],"amber")]:
        d.box(x,115,250,120,title,lines,tone)
    d.arrow(275,175,325,175); d.arrow(575,175,625,175)
    d.box(175,285,550,110,"④ Endpoint → ISender → 应用命令",["解析 If-Match：格式错误 / 缺失为 428","应用当前角色检查、验证、幂等与领域执行"],"teal")
    d.path("M750 235V260H450V285")
    d.write("10-aspnet-request-pipeline.svg", "JSON 绑定与端点过滤器属于端点调用链；不能把反伪造 filter 画成总在正文绑定之前。")

    d = Sheet("进入按注册顺序，成功返回沿相反方向", "Logging 是先执行的 RequestPreProcessor，不是下面的自定义行为", "Performance 不包住前面的授权/验证；幂等存储而非 MediatR 自动拥有事务", 485)
    rows = [("UnhandledException", "包住后续自定义行为；记录并重新抛出", "navy"),("Authorization", "查询当前角色与锁定状态；失败就停止", "purple"),("Validation", "验证字段与范围；失败不进入业务事务", "amber"),("Performance", "从 next 开始计时；成功返回后检查慢请求", "navy"),("Idempotency", "新意图：事务/锁/回执；已有：历史回放", "teal"),("Handler + Domain", "协调持久化 / 执行业务不变量", "teal")]
    for i,(title,detail,tone) in enumerate(rows):
        y=102+i*53
        fill,accent=PALETTE[tone]
        d.parts.append(f'<rect x="30" y="{y}" width="790" height="43" rx="6" fill="{fill}"/>')
        d.text(43,y+28,title,18,accent,True); d.text(290,y+28,detail,17)
    d.arrow(850,110,850,405); d.text(834,432,"进入",16)
    d.write("11-mediatr-pipeline.svg", "按本项目 DI 与 MediatR 12.5.0 的注册/包装实现读链；日志最小化、授权、计时和事务边界各不相同。")

    d = Sheet("当前指针可以失效，业务历史不能覆盖", "聚合边界保护同一案件的调查写入；来源资料通过告警引用", "Version 标识业务动作；BasisVersion 标识被评估的调查事实基础")
    d.box(80,105,740,105,"RiskCase：状态 / 经办人 / Version / BasisVersion",["CurrentAssessmentId → 当前判断；ReviewedAssessmentId → 本次提交对象"],"navy")
    for x,title,lines,tone in [(25,"笔记与关系",["Added：事实追加","使当前评估引用失效"],"teal"),(325,"Assessments",["作者 / 理由 / 两种版本","不可变历史集合"],"amber"),(625,"Decisions",["独立复核人 / 理由","引用 AssessmentId"],"purple")]:
        d.box(x,270,250,110,title,lines,tone)
        d.arrow(x+125,210,x+125,270)
    d.write("12-riskcase-aggregate.svg", "对象所有权与当前引用有不同语义：新增调查事实清空投影，历史判断仍可重建。")

    d = Sheet("不同检查产生不同的失败语义", "读取全案与写入经办案件分开判断；角色声明不等于当前资格", "界面提示不能替代下列任一服务器边界", 440)
    rows=[("身份 / 端点策略", "Cookie 主体与 API policy", "401 / 403", "purple"),("当前角色 / 输入", "Identity 锁定状态；Validator 字段范围", "403 / 400", "amber"),("新写入资源规则", "预期版本、当前经办人与动作资格", "412 / 403", "navy"),("领域 / 数据库", "状态、独立性、唯一性与并发写入", "409 / 412", "teal")]
    for i,(title,detail,code,tone) in enumerate(rows):
        y=105+i*69; fill,accent=PALETTE[tone]
        d.parts.append(f'<rect x="25" y="{y}" width="850" height="57" rx="7" fill="{fill}"/>')
        d.text(39,y+25,title,18,accent,True); d.text(255,y+25,detail,17); d.text(730,y+25,code,17,accent,True)
        d.text(255,y+47,"失败即停止；后续层不会补救前层拒绝。",15)
    d.write("13-request-guardrails.svg", "角色、格式、资源关系与领域不变量分工。幂等回放也先经过当前角色检查；新写入才继续资源校验。")

    d = Sheet("同一写者，两个读事务的可见性对照", "横向是时间；泳道彼此独立，GET 不会调用并发写者", "独立 GET 用 RR；已有 RC 写事务的详情回读另用父行 FOR SHARE 锁", 470)
    for x,t in [(175,"t1 首次数据读"),(455,"t2 并发提交"),(735,"t3 后续数据读")]:
        d.text(x-15,108,t,16,"#22364f",True); d.path(f"M{x} 120V410", "#dbe5ea", marker=False,dash=True)
    for y,label in [(145,"RC 读者"),(245,"独立写者"),(345,"RR 读者")]:
        d.text(25,y+30,label,17,"#22364f",True);d.path(f"M145 {y+35}H875",marker=False)
    d.box(155,132,200,76,"案件 v5",["语句快照 S1"],"navy");d.box(665,132,210,76,"审计 v6",["新语句快照 S2"],"coral")
    d.box(360,235,265,76,"COMMIT v6",["案件、子记录、审计原子提交"],"amber")
    d.box(155,332,200,76,"案件 v5",["固定快照 S"],"teal");d.box(665,332,210,76,"集合 / 审计 v5",["继续使用 S"],"teal")
    d.arrow(355,170,665,170);d.arrow(355,370,665,370)
    d.write("round3-consistent-snapshot.svg", "RC 两条正确 SELECT 仍可能拼成混合版本；RR 在第一次数据语句固定快照，独立写者仍可提交 v6。")

    d = Sheet("恢复历史成功后，还要读取当前状态", "K 包含原意图；转派后的当前 v6 不能被历史 v5 覆盖", "每次请求先查当前角色；原键回放不授权新写入", 475)
    d.box(25,110,250,115,"首次发送 K / If-Match 4",["先持久化原请求与草稿代次","新意图执行评估"],"navy")
    d.box(325,110,250,115,"一次 COMMIT → v5",["评估 + 审计 + 回执 K","此后响应丢失，客户端未知"],"teal")
    d.box(625,110,250,115,"主管转派 → 当前 v6",["当前评估投影清空","评估历史 A1 仍在"],"amber")
    d.arrow(275,168,325,168);d.arrow(575,168,625,168)
    d.box(25,290,250,105,"刷新后沿原键恢复",["原正文 + If-Match 4","不换键、不改前置条件"],"purple")
    d.box(325,290,250,105,"读取回执 K → 历史 v5",["不再次调用 Handler","不再新增评估和审计"],"navy")
    d.box(625,290,250,105,"React 协调并 GET",["保留较新 v6 缓存","草稿按代次保护"],"teal")
    d.arrow(275,342,325,342);d.arrow(575,342,625,342)
    d.path("M450 225V290", "#7255a5",dash=True)
    d.write("16-concurrency-and-idempotency.svg", "上排是首次提交与后续转派，下排是同意图恢复。虚线表示读取原回执，不能把历史快照当当前事实。")

    d = Sheet("生成链负责合同，适配层负责请求策略", "端点与 DTO 是机器可读形状来源；运行时语义仍需实际请求验证", "手写 adapter 不重复 DTO；重新生成后检查差异不能代替权限及恢复测试")
    d.box(25,115,250,140,"ASP.NET Core",["真实路由与 DTO","/openapi/v1.json","枚举 / nullable / headers"],"navy")
    d.box(325,115,250,140,"NSwag → TypeScript",["src/generated/","riskcase-api.ts","URL、序列化与 DTO"],"teal")
    d.box(625,115,250,140,"lib/api.ts",["Cookie + CSRF","持久化原意图及重试","调用生成的 clients"],"purple")
    d.arrow(275,185,325,185);d.arrow(575,185,625,185)
    d.box(100,310,700,65,"Resume 正文可空：兼容旧成功回放；新操作必须有有效原因",[],"amber")
    d.write("18-openapi-nswag.svg", "合同形状与业务许可分别验证。Reason 的 nullable 是旧回放通道，不是新业务省略理由的许可。")

    d = Sheet("路由树与页面状态各有所有者", "箭头表示包含或拥有，不表示 Query 会顺序调用 UI 组件", "账号 / 案件 key 使 CaseWorkspace 重建；底层 UI 组件不持有服务器业务事实")
    d.box(25,180,210,105,"SignedInApp",["session + QueryClient","登录 / 注销边界"],"navy")
    d.box(325,180,230,105,"AppShell + Routes",["导航、身份、路由出口","统一 UI 组件"],"teal")
    d.box(650,105,225,105,"QueuePage",["筛选 / 分页查询","当前列表视图"],"amber")
    d.box(650,280,225,105,"CaseWorkspace",["案件 Query / mutations","本地草稿与待恢复意图"],"purple")
    d.arrow(235,232,325,232);d.path("M555 232H600V157H650");d.path("M600 232V332H650")
    d.write("19-react-application-structure.svg", "QueuePage 直接加载；案件路由的 lazy/Suspense 延后加载包含 React Flow 的工作区。")

    d = Sheet("从业务身份映射到固定的图形视图", "这是展示映射；当前实现没有节点详情面板或逐边 URL 跳转", "节点不可拖动、不可新连线；缩放、平移与选中不改变持久化事实")
    d.box(25,110,250,145,"API 合同",["graphNodes: id/type/detail","graphEdges: source/target","rationale 保留手工理由"],"navy")
    d.box(325,110,250,145,"RiskGraph useMemo",["type → 固定列位置","同类计数 → 固定行位置","真实 ID 传给节点与边"],"teal")
    d.box(625,110,250,145,"React Flow",["节点类型标签 + 摘要","交易边与紫色手工边","draggable:false"],"purple")
    d.arrow(275,180,325,180);d.arrow(575,180,625,180)
    d.box(100,310,700,65,"完整交易事实在调查标签页；邻近位置不等于共同控制",[],"amber")
    d.write("21-react-flow-graph.svg", "UI 只派生坐标和样式，保留后端稳定身份。用已实现的标签/摘要读图，不假设尚不存在的详情交互。")

    d = Sheet("同一案件 ID，逐次版本确认调查基础", "下列版本对应本轮真实浏览器工作流；A 为经办人，R 为独立主管", "每次新写入有各自 UUID 键与当前 If-Match；历史评估不被覆盖", 485)
    top=[("v1 → v3",["接收 → 分派给 A","A 开始调查"],"navy"),("v4 → v5",["A 写笔记","A 评估 High"],"amber"),("v6 Reviewing",["A 提交评估 A1","调查事实冻结"],"purple")]
    bottom=[("v7 → v8",["R 有理由退回","A 补充核验笔记"],"amber"),("v9 → v10",["A 评估 Medium","重提，引用新评估 A2"],"teal"),("v11 Resolved",["R 独立核对 A2","记录下游升级建议"],"navy")]
    for row,y in [(top,110),(bottom,295)]:
        for i,(title,lines,tone) in enumerate(row): d.box(25+i*300,y,250,115,title,lines,tone)
        d.arrow(275,y+57,325,y+57);d.arrow(575,y+57,625,y+57)
    d.path("M750 225V255H150V295")
    d.write("22-analyst-workflow.svg", "退回使当前评估失效；v9 的新评估与 v5 同时保留，v11 的决定引用新的本次复核对象。")

    d = Sheet("每个测试层都应能推翻一种错误承诺", "生产代码不引用测试屏障；CI 运行测试，但不改变这些证明边界", "只断言 HTTP 失败不够：要独立核对失败后没有部分子记录、审计和回执")
    d.box(25,110,250,175,"领域 / 应用单元",["输入：对象与身份抽象","证明：状态、资格、作者规则","反例：转派后自我复核","不能证明 PostgreSQL 锁"],"navy")
    d.box(325,110,250,175,"真实 PostgreSQL",["输入：实际 API 与数据库","证明：约束、回滚、MVCC","反例：同版两写同时提交","屏障 + 独立 SQL 验证"],"teal")
    d.box(625,110,250,175,"真实浏览器",["输入：应用服务与合成数据","证明：草稿、响应次序、恢复","反例：ABA 后旧回调清空","不能替代数据库原子性"],"purple")
    d.box(100,330,700,50,"断言来自业务保证，而不是测试数量或是否出现绿色日志",[],"amber")
    d.write("23-testing-pyramid.svg", "三层各自使用真实边界与反例。测试专用屏障创造竞争条件，独立读取确认最终提交事实。")

    d = Sheet("按结果是否已知选择恢复动作", "先识别业务错误 code，再决定读新状态、改输入或保留原意图", "401 / 403 可能发生在恢复阶段，不能反推第一次提交没有成功", 485)
    rows=[("网络中断 / 503", "未知或数据库竞争中止：保留原键、正文与 If-Match", "amber"),("412 version_conflict", "新操作基于旧版本：GET，再由人确认新的意图", "coral"),("409", "区分键误用、重复关系和领域状态；不能统一换键重试", "purple"),("400 / 428", "修正输入 / 前置条件；确认原意图的明确失败语义", "navy"),("401 / 403", "恢复身份或检查当前角色与资源资格；不绕过权限", "teal")]
    for i,(title,detail,tone) in enumerate(rows):
        y=105+i*64;fill,accent=PALETTE[tone]
        d.parts.append(f'<rect x="25" y="{y}" width="850" height="52" rx="7" fill="{fill}"/>')
        d.text(40,y+32,title,18,accent,True);d.text(270,y+32,detail,17)
    d.write("24-failure-recovery.svg", "相同的重试按钮不能解决所有失败。先分清未知结果、明确冲突和资格拒绝，再选择恢复方式。")

    path=ROOT / "publication-captions.json"
    existing=json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
    existing.update(CAPTIONS)
    path.write_text(json.dumps(existing,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(f"Updated {len(CAPTIONS)} source-grounded Round 4 diagrams.")


if __name__ == "__main__": build()
