# RoadLane 通用导航网络设计

## 系统边界

VehicleRoads 使用 Unity Splines 作为编辑态 Lane 几何来源，并将结果烘焙到 Schema 3.1
`BakedLaneNetwork`。运行时只读取该 ScriptableObject，不读取 Spline、DataTable 或 NavMesh。

- VehicleRoads 源码不得引用 `UnityEngine.AI`、`Unity.AI.Navigation` 或 NavMesh 运行时 API。
- 项目中的旧 PedestrianNPC 本期保持不动，因此暂不删除 Unity Navigation 包。
- 不提供 NavMesh fallback、桥接组件或适配器。
- 不修改 BlueprintSystem，也不新增 Blueprint/Behavior Tree 节点。
- 不负责局部避障、动态障碍绕行、物理移动或跨 `BakedLaneNetwork` 寻路。

`VehicleRoadSubsystem` 注册一个或多个烘焙网络，提供统一空间查询、路线、交通运行时状态、
Profiler/诊断计数器和公开调试快照。现有车辆 API 保留，并转发或继续读取同一份烘焙资产。

## Schema 3.1 ScriptableObject

`BakedLaneNetwork` 是唯一运行时权威数据。Bake 原位更新目标 `.asset`，已有资产保持 GUID。
资产包含：

- 有向 Lane/Connector、采样、连接、相邻换道链接和交通控制记录。
- Lane 中心、左右边界、姿态、曲率、累计距离、局部宽度、最小/最大宽度、Bounds、Tag Mask 和 Agent Mask。
- Polygon Zone 顶点、三角形、Bounds、高度范围、标签、Agent 和开放状态。
- Portal 的方向、宽度、成本、开放状态、标签、Agent、源/目标位置。
- Lane 线段和 Polygon Bounds 的三维空间索引及烘焙摘要。
- 显式引用的 `RoadNetworkSettings` 与 `RoadNetworkRuntimeSettings`。

VehicleRoads 不再输出或读取 `.bpdatatable.json` 或结构 JSON。BlueprintSystem
其他业务 DataTable 不受影响。

## Lane、宽度与边界

每条 `RoadLane` 对应独立 GameObject、开放 `SplineContainer` 和 `RoadLane` 组件。

- 手绘 Lane 默认宽度为 3.5m；当前每条 Lane 使用恒定宽度。
- Standard Lane 可为正向、反向或双向；双向 Bake 为两个稳定 ID 的有向 Lane。
- Connector 固定单向，默认宽度取入口、出口 Lane 的较小值，也可人工覆盖。
- 每个采样点写入中心、左右边界、Forward、Up、曲率和累计距离。
- 急弯导致边界翻转、自交或有效宽度不足时，Validation/Bake 将其视为阻断错误。
- 相邻换道关系保持独立于纵向 Connection；Profile 的左右换道配置优先于几何推断。

点、Sphere 和 Bounds 查询通过空间索引执行，并返回统一 `RoadLocation`：

- 元素类型与稳定 ID。
- 世界输入位置和网络投影位置。
- Lane 沿程距离、横向比例、边界距离。
- Polygon 三角形索引。
- 高度差、是否位于有效边界内和明确失败原因。

## Lane Profile

`RoadLaneProfile` 描述道路横断面模板。每个 Entry 包含永久稳定 `entryId`、宽度、方向、
限速、Tag Mask、Agent Mask、开放状态、连接策略和左右换道权限。

`RoadLaneProfileSource` 使用一条参考 Spline，并支持 `Center`、`LeftEdge`、`RightEdge` 对齐。
每个参考 Spline Knot 对应稳定 `RoadLaneProfileControlPoint`，可覆盖 Profile 或强制拓扑断点。
相邻控制点按 Entry ID 匹配：持续 Entry 平滑插值中心与宽度，新增/删除 Entry 从最近存续
Entry 分流或合流；无存续 Entry 时在控制点终止。生成 Lane 使用局部宽度关键点，Bake 后查询、
边界和 Agent 半径过滤均读取采样点局部宽度。
Apply 或 Bake 前刷新时：

- 以 `{SourceId}_{EntryId}` 生成稳定 Lane ID。
- 未锁定受管 Lane 自动刷新宽度、属性和偏移 Spline。
- 锁定 Lane 保留人工修改。
- 已删除 Entry 对应 Lane 标记 `ManagedProfileOrphaned` 并关闭，不自动删除。
- 未使用控制点覆盖的 Profile Source 保持 `{SourceId}_{EntryId}` ID；启用沿程变化后使用
  `{SourceId}_{EntryId}_{RunStartPointId}`，强制断点两侧保持独立稳定运行段。

## 自动拓扑与实时预览

`Road Lane Authoring` 的 Draw Lane 和 Spline Edit 支持端点释放后的自动拓扑：

- 兼容的出口/入口端点吸附后使用 Automatic Connection。
- 转向、分支或多端点汇聚时创建或复用 `RoadJunction` 并刷新未锁定 Connector。
- 命中普通 Lane 中段时使用 Bezier 精确拆分；原 ID 留给起点段，新段获得稳定 split ID。
- 命中 Profile 管理 Lane 时回到参考 Spline 插入控制点和强制断点，再刷新整组 Lane。
- Junction Binding、Portal 和 Lane 尾部 Manual Next 引用随拆分更新；锁定与 Orphaned 规则不变。
- 单次吸附、拆分、引用更新和 Junction 创建属于同一个 Undo 事务。

Live Network Preview 监听 Spline、层级和 Undo 变化，防抖 250ms 后生成
`HideAndDontSave` 临时网络并绘制 Lane 边界、连接和相邻关系。非法网络只清除临时预览并显示
校验问题。Play Mode 不自动重建，正式 `.asset` 仍只由 **Bake Network Asset** 原位更新。

## 强类型标签与 Agent

`RoadTagMask` 和 `RoadAgentMask` 均为 32 位掩码。默认 Agent 位包括 Car、Truck、Bus、
Emergency、Service、Bicycle 和 Pedestrian。

`RoadTagFilter` 使用三组组合条件：

- `all`：所有位必须存在。
- `any`：非空时至少命中一位。
- `none`：不得命中任意一位。

项目级 `RoadNetworkSettings` 固定每一位的含义和颜色。已使用位不得因列表排序改变。
运行时判断使用强类型掩码。Lane 和 Portal 还会检查 Agent 直径是否小于等于有效通行宽度。

## Polygon Zone 与 Portal

`RoadPolygonZone` 使用局部 XZ 顶点及垂直高度范围。支持简单凸形和凹形；自交、多环和洞
会被拒绝。Bake 使用耳切法三角剖分并生成三角形邻接。

`RoadPortal` 必须位于 Polygon 子层级，可连接 Lane 端点或另一个 Polygon Portal，并配置
宽度、方向、成本、开放状态、Tag Mask 和 Agent Mask。

统一路线支持：

- Lane → Polygon → Lane。
- Polygon → Polygon。
- Polygon 内起终点。

拓扑搜索在 Lane/Polygon 节点间执行；Polygon 内部路径通过三角形邻接和漏斗算法生成。

## Road Agent

`RoadAgent` 是通用路线状态机和控制输出组件。它接受调用方提供的当前位置、朝向、速度和
目标，不读取或修改自身 `Transform`，也不依赖 Rigidbody、CharacterController 或
NavMeshAgent。

状态使用明确枚举：

- Agent：Idle、Planning、Following、Replanning、Arrived、Suspended、Failed。
- Route：None、Pending、Valid、Partial、Invalid、Cancelled。
- Failure：区分网络缺失、无元素、过滤失败、宽度不足、Portal 关闭、无拓扑、越界等。
- Element：Lane、Connector、Polygon、Portal。

输出包含目标位置/姿态、目标速度、剩余距离、当前元素、边界距离、恢复建议和失败原因。
外部执行器负责实际移动和局部避障。

`VehicleLaneFollower` 继续负责 Pure Pursuit、车辆跟随、信号、排队和换道。车辆可消费统一
路线；只有 Lane/Connector 段应用车辆交通规则，Polygon 段不应用路口规则。

## Profiler、诊断与 AI 调试

`Project Settings > Vehicle Road > Road Network` 编辑两份项目级设置资产：

- `RoadNetworkSettings.asset`：标签位和 Agent 位。
- `RoadNetworkRuntimeSettings.asset`：Profiler 和诊断历史开关。

运行时设置包含：

- Enable Runtime Profiler Markers。
- Enable Detailed Diagnostic History。
- Diagnostic History Capacity（16–2048，默认 128）。
- Capture Successful Queries。
- Capture Failed Queries。
- Capture Agent State Transitions。
- Development Build Diagnostics。

Editor Play Mode 和 Development Build 可启用详细诊断；非 Development Release Build
强制关闭 Profiler 和历史。设置缺失时全部关闭。Marker 使用固定静态名称，关闭时只返回
空采样作用域，不格式化 ID 或字符串。

Profiler 覆盖 Lane/边界 Bake、Profile 刷新、Polygon 三角剖分、空间索引、空间查询、
过滤、路线搜索、Polygon 漏斗、Agent 评估/重规划、前车、换道和路口控制。

核心只读快照始终可用：

- Subsystem 网络、Lane、Polygon、Portal、Agent、车辆、队列、Token、换道预约统计。
- 每帧查询、路线、重规划和失败计数。
- 最近/峰值候选数、访问节点数和路线段数。
- Agent 状态、目标、当前元素、路线段、剩余距离、速度意图和失败原因。
- 最近查询与最近路线的结构化快照。
- 调用方缓冲区复制的固定容量环形历史。
- 有行数上限的紧凑文本报告。

关闭详细历史后不写事件、不创建事件对象、不格式化诊断字符串。运行时检查应使用这些公开
API，不使用反射或复杂组件全量序列化。

## 验收规则

- Spline 至少两个 Knot，长度大于 1cm，所有位置和方向必须为有限数。
- Lane ID、Profile Entry ID、Polygon ID 和 Portal ID 稳定且唯一。
- Summary 中 invalid Lane/Polygon/Sample 数量为零。
- 常规运行时空间查询使用索引，不全量遍历所有元素。
- Connector、锁定和 Orphaned 规则保持幂等。
- 车辆信号、队列、前车、通行 Token 和换道回归通过。
- VehicleRoads 目录静态扫描不包含 Navigation API。
- DemoScene Bake 只更新版本化 `.asset`，不生成 VehicleRoads JSON。
