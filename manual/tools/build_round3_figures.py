"""Source-grounded Round 3 mechanisms; editable SVGs in the existing visual grammar."""
from build_round2_figures import Diagram


def build():
    d = Diagram("历史作者可恢复，旧理由不能补造", "审计身份索引与完整评估记录承担不同职责", "已知作者参与过任一评估 → 禁止最终复核；任一身份缺口 → 无法证明独立性")
    d.box(30,115,255,126,"旧 AssessmentRecorded",["v4 · ActorId=S · High","v6 · ActorId=A · Low","早期完整理由已经覆盖"],"navy")
    d.box(335,115,255,126,"不可变参与记录",["CaseId / CaseVersion / 作者","SourceAuditId 保留来源","不伪造 RiskAssessment"],"teal")
    d.box(640,115,225,126,"最终复核人 R",["检查所有新旧作者","R 不能是 S 或 A","经办人变化不清除历史"],"purple")
    d.arrow(285,178,335,178); d.arrow(590,178,640,178)
    d.box(30,300,400,114,"保留的最后一次评估快照",["IsLegacy=true · 原等级 / 理由 / 时间","作者索引独立保存可核实的审计身份"],"amber")
    d.box(475,300,390,114,"审计或身份确实缺失",["审计 ID / 评估快照 ID → 作者 NULL","保持未知并阻止结案，不猜当前负责人"],"coral")
    d.write("round3-legacy-author-recovery.svg")

    d = Diagram("分拆查询必须共享一个可见性边界", "示意：GET 读取期间，另一事务将案件从 v5 提交为 v6", "独立 GET：REPEATABLE READ；已有写事务：读取前 FOR SHARE 主行锁，复用事务")
    d.text(35,111,"原实现 · 每条语句建立新快照",18,"#b84940",True)
    d.box(35,140,225,90,"案件查询 S1",["案件 v5 · 集合 v5"],"navy")
    d.box(335,140,225,90,"并发写事务提交",["案件 v6 + 审计 v6"],"amber")
    d.box(635,140,230,90,"审计查询 S2",["看到 v6 → 响应矛盾"],"coral")
    d.arrow(260,185,335,185); d.arrow(560,185,635,185)
    d.text(35,283,"独立 GET 修复后 · 第一次查询固定快照 S",18,"#117c78",True)
    d.box(35,312,225,90,"案件查询 S",["案件 v5"],"teal")
    d.box(335,312,225,90,"并发仍可提交 v6",["不进入此 GET 的快照"],"amber")
    d.box(635,312,230,90,"集合与审计仍用 S",["都看到 v5，可一致组装"],"teal")
    d.arrow(260,357,335,357); d.arrow(560,357,635,357)
    d.write("round3-consistent-snapshot.svg")

    d = Diagram("一次有理由的退回开始新的调查基础", "返回不是删除评估；它使当前评估和复核引用失效", "原因去除首尾空白后为 20-1500 个 Unicode 字符；理由、操作者、时间与 vN+1 一起提交")
    d.box(30,125,245,115,"Reviewing · vN",["调查事实已冻结","当前提交指向评估 A1"],"purple")
    d.box(330,125,245,115,"确认退回原因",["具体核验缺口与待办","If-Match + 原幂等键"],"amber")
    d.box(630,125,240,115,"Investigating · vN+1",["当前评估引用清空","新 BasisVersion=vN+1"],"teal")
    d.arrow(275,180,330,180);d.arrow(575,180,630,180)
    d.box(30,315,395,105,"不可变审计 InvestigationResumed",["Reason / ActorId / OccurredAt / 版本","旧评估 A1 仍保留完整历史"],"navy")
    d.box(485,315,385,105,"重新评估 A2 → 再提交复核",["明确新依据，ReviewedAssessmentId=A2","主管不能直接沿用已失效的 A1"],"teal")
    d.arrow(450,240,450,285);d.arrow(450,285,225,315);d.arrow(750,240,750,315)
    d.write("round3-review-return-loop.svg")

    d = Diagram("把集合乘积改成固定条数的读取", "小型合成案件：10 笔交易 / 3 个信号 / 5 条笔记 / 3 条关系 / 2 次评估", "9 条详情 SQL 的一致性由事务边界保障；集合仍完整返回，数据量继续按记录总数增长")
    d.box(30,115,395,152,"单查询加载（原实现）",["交易 × 信号 × 笔记 × 关系 × 评估","10 × 3 × 5 × 3 × 2 = 900 行","同一主记录反复携带到客户端","另查 1 行审计：实际共 901 行"],"coral")
    d.box(475,115,390,152,"分拆加载（当前实现）",["主记录 + 7 个集合查询 + 审计","集合：交易 / 信号 / 笔记 / 关系","决定 / 完整评估 / 旧作者参与记录","小型实际根输出行数之和：25"],"teal")
    d.arrow(425,190,475,190)
    d.box(30,315,395,105,"20 次端点中位数 / p95",["原实现：146.1 / 179.3 ms","当前实现：25.5 / 43.1 ms"],"navy")
    d.box(475,315,390,105,"测量条件与代价",["本机 PostgreSQL 17.12 · Release","TestServer 含认证；不含网络延迟"],"amber")
    d.write("round3-query-shape.svg")

    d = Diagram("聚合规则需要完整的新旧作者事实", "写入加载与只读详情采用不同读取策略", "EF 状态负责生成 SQL；数据库约束与外层事务决定能否作为一次完整事实提交")
    d.box(30,120,385,150,"可写聚合加载",["Links：已有端点对","Assessments：完整评估作者","LegacyAssessorParticipation：旧作者","先核对版本与当前资源权限"],"navy")
    d.box(485,120,385,150,"只读案件详情",["AsNoTracking + AsSplitQuery","独立 GET：REPEATABLE READ","已有写事务：先锁案件主行","9 条详情查询，不做 N+1 循环"],"teal")
    d.box(30,325,840,92,"新增记录必须明确标记 Added",["INSERT 子记录 + INSERT 审计 + UPDATE 案件版本 + 幂等回执，同一事务成功或回滚"],"amber")
    d.write("14-ef-change-tracking.svg")


if __name__ == "__main__":
    build()
