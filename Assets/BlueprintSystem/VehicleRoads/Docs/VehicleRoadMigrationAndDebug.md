# RoadLane 制作与调试流程

## 制作入口

### Lane 和 Junction

`Road Lane Authoring` 提供 Select、Draw Lane、Junction、Spline Edit、Profile 和 Polygon：

- Select 只改变选择，不修改绑定或拓扑。
- Draw Lane 创建 `SplineContainer + RoadLane`。
- Junction 绑定 Lane 端点并生成可编辑 Connector。
- Spline Edit 修改 Knot，可使用贴路、拉平和 XZ 网格吸附。
- 相邻复制和 Adjacent Link 预览仍保留。
- Profile 模式选择参考 Spline Knot，并设置 Profile Override 或 Force Topology Break。
- Polygon 模式制作 `RoadPolygonZone` 和 `RoadPortal`：草稿顶点创建 Zone，拖动顶点编辑边界，
  边中点插入顶点，高度手柄编辑体积，Portal 创建和拖动时吸附到边界。
- Endpoint Snap 在 Draw Lane 完成或 Spline Edit 端点松开时执行；可配置端点/中段半径、
  Direct Connect Angle 和 Auto Junction。
- Live Network Preview 默认启用，编辑停止 250ms 后只重建隐藏临时网络；正式资产仍需手动 Bake。

### Lane Profile

1. 在 `RoadLaneNetwork` 子层级创建带 `SplineContainer` 的对象。
2. 添加 `RoadLaneProfileSource` 并设置稳定 Source ID。
3. 创建/指定 `RoadLaneProfile`，为每条横断面 Lane 设置稳定 Entry ID。
4. 选择 Center、LeftEdge 或 RightEdge 对齐。
5. 点击 **Apply Lane Profile**。

也可在 **Profile** 模式逐 Knot 指定 Profile。相同 Entry ID 会跨控制点连续生成；宽度变化、
车道新增和车道消失会生成渐变与分流/合流。需要在某个 Knot 切断拓扑时勾选
**Force Topology Break**。

未锁定受管 Lane 会刷新；锁定 Lane 保留人工修改；删除 Entry 后对应 Lane 变为 Orphaned，
需要人工确认后再决定是否删除。

### Polygon 和 Portal

1. 在 `Road Lane Authoring` 切到 `Polygon`。
2. 左键放置草稿顶点，`Enter` 或右键创建 `RoadPolygonZone`。
3. 通过顶点、边中点和高度手柄编辑边界及体积；Inspector 会实时显示三角剖分是否合法。
4. 打开 `Create Portal On Boundary`，在 Active Zone 边界创建 `RoadPortal`。
5. 拖动 Portal 时会自动投影到最近边界。
6. 查看最近 Lane 端点或另一个 Polygon Portal 的建议目标，点击 `Apply Suggested Target` 后才写入引用。
7. Validate 后 Bake。

自交 Polygon、洞、多环、无目标 Portal、宽度不足或不在有效边界上的 Portal 应先修正。

## Project Settings

打开 `Project Settings > Vehicle Road > Road Network`：

- 首次使用时创建 `RoadNetworkSettings.asset` 和
  `RoadNetworkRuntimeSettings.asset`。
- 在 `RoadLaneNetwork` Inspector 点击 **Assign Project Road Network Settings**。
- 在 `VehicleRoadSubsystem` Inspector 点击
  **Assign Project Runtime Diagnostics Settings**。

未绑定运行时设置时，Profiler 和详细历史保持关闭，但核心当前快照仍可读取。

## 查询与运行调试

打开 `Tools > Blueprint System > Vehicle Road > Road Network Runtime Debug`：

- 指定 `VehicleRoadSubsystem` 或单个 `BakedLaneNetwork`。
- 输入 Agent Mask、半径及 Tag `all/any/none`。
- 执行最近元素或统一路线查询。
- 查看网络、Agent、队列、Token、查询/路线计数和历史占用。
- 按稳定 Agent ID 获取目标 Agent 快照。
- 复制有上限的紧凑报告。

## 分层排查

### 1. 源数据与 Bake

- 检查 Spline 是否开放、至少两个 Knot，且无 NaN/Infinity。
- 检查 Lane 宽度、左右边界、急弯翻转和最小有效宽度。
- 检查 Profile Entry ID、锁定和 Orphaned 状态。
- 检查 Polygon 自交、三角形、Bounds、高度范围和 Portal 连接。
- 检查 Bake 后 Schema 为 3.1，Summary invalid 计数为零。
- 检查每个 Sample 的局部宽度，以及 Lane 的 minimum/maximum width。
- 自动拆分后检查原 ID 是否保留在起点段，Junction/Portal/Manual Next 引用是否更新。

### 2. Subsystem 查询与路线

- 确认目标 `.asset` 已注册，且 Lane/Polygon/Portal ID 无冲突。
- 检查 Agent Mask、Agent 半径和 Tag Filter。
- 立交选错层时检查 `heightDifference` 和最大高度差。
- 查询失败时读取最近查询快照的候选数、命中和失败原因。
- 路线失败时读取起终元素、访问节点数、路线段数和失败原因。
- 当前不支持跨多个 `BakedLaneNetwork` 拼接路线。

### 3. RoadAgent/Follower

- RoadAgent 不移动对象；先检查其控制输出，再检查外部执行器。
- 检查 Agent state、route state、当前元素、segment index、remaining distance、
  target speed、distance to boundary 和 failure reason。
- 车辆画面异常时继续区分：
  `VehicleRoadSubsystem` 交通控制、`VehicleLaneFollower.LastOutput`、Demo 执行器和 Gizmo。
- Polygon 段不应用信号、队列和换道；Lane/Connector 段才应用车辆交通规则。

### 4. 车辆交通

- 红灯/停车：检查 `stopPoint`、`distanceToStopLine`、`queueIndex`、`signalState`。
- 跟车：检查前车 ID、距离、速度和当前显式路线。
- 换道：检查 Adjacent Link、Profile 左右权限、目标 Lane 开放状态、安全窗口和预约。
- 通行授权：检查队首、信号相位、冲突 Connector 和活动 Token。

上游输出正确但对象未执行时，应修改执行器；只有查询/规划输出本身错误时才回到
Subsystem、路线或源数据。

## Profiler 与历史

Profiler Marker 使用固定名称，可在 Editor Play Mode 或 Development Build 中开启。
Release Build 强制关闭详细 Profiler 和历史。

建议只在定位时开启成功查询历史；失败查询和 Agent 状态转换可保持默认开启。环形缓冲区
满后覆盖最旧事件，并公开 dropped count。关闭历史后不会写事件或格式化诊断文本。

## 自动化验证

VehicleRoads EditMode 测试覆盖：

- Lane 正反向、边界、宽度、立交高度和空间查询。
- Tag `all/any/none`、Agent Mask 和半径过滤。
- Profile 稳定 ID、锁定和 Orphaned。
- 凸/凹 Polygon、非法自交和 Polygon 漏斗路径。
- Lane → Polygon → Lane 混合路线。
- RoadAgent 规划、输出、取消和结构化快照。
- 环形历史容量、覆盖顺序和缺失设置的安全默认值。
- Connector、A*、动态封路、信号、排队、前车、Token、换道和 Follower 回归。
- VehicleRoads 对 Navigation API 的静态隔离。

运行测试后同时检查 Unity Console 新增 Error；不要把 MCP 组件全量序列化产生的
`ValidTRS()` 工具噪声误判为道路系统错误。
