# Vehicle Roads 使用文档

本文面向在场景里制作、烘焙、接入和排查车辆道路系统的人。更底层的设计约束见
`Assets/VehicleRoads/Docs/VehicleRoadSystemDesign.md`，制作和分层排查规则见
`Assets/VehicleRoads/Docs/VehicleRoadMigrationAndDebug.md`。

## 1. 核心概念

Vehicle Roads 的数据链路是：

1. 编辑态：`RoadLaneNetwork` 下的 `RoadLane`、`RoadJunction`、`RoadPolygonZone`、
   `RoadPortal`、`RoadLaneProfileSource`。
2. 烘焙态：`BakedLaneNetwork.asset`。
3. 运行态：`VehicleRoadSubsystem` 注册 `BakedLaneNetwork` 并提供查询、路线、交通控制、
   队列、信号、换道和诊断快照。
4. 车辆控制输出：`VehicleLaneFollower` 读取道路和交通状态，输出转向、目标速度、停止点、
   信号状态、队列序号和换道状态。
5. 执行器：车辆自己的移动/物理脚本消费 Follower 输出并实际移动对象。

重要边界：

- 运行时权威数据只有 `BakedLaneNetwork.asset`。
- 运行时不读取 Spline、场景里的编辑组件、DataTable 或 NavMesh。
- `Live Network Preview` 只是隐藏的临时预览，不会修改正式资产。
- Play Mode 不会自动重建道路；改完路必须手动 `Bake Network Asset`。
- Vehicle Roads 模块不得依赖 `UnityEngine.AI` 或 Unity Navigation 运行时 API。
- 当前路线查询只支持同一个 `BakedLaneNetwork` 内部，不做跨网络路线拼接。

## 2. 首次创建道路网络

### 2.1 创建 Network

推荐入口：

- 菜单：`GameObject > Vehicle Road > Vehicle Road Network`
- 或手动创建空物体后添加 `Vehicle Road/Road Lane Network`

选中 `RoadLaneNetwork` 后先检查这些字段：

- `Sample Spacing`：采样间距。常规道路可先用 `1`。
- `Connection Radius`：自动连接端点的半径。
- `Connection Direction Tolerance`：端点方向允许角度。
- `Output Asset Path`：烘焙资产输出路径，建议放在 `Assets/VehicleRoads/Generated/`。
- `Network Settings`：项目级道路标签和 Agent 位配置。
- `Runtime Settings`：运行时诊断、Profiler 和历史记录配置。

如果设置资产还没绑定：

1. 打开 `Project Settings > Vehicle Road > Road Network`。
2. 创建或指定 `RoadNetworkSettings.asset` 和 `RoadNetworkRuntimeSettings.asset`。
3. 回到 `RoadLaneNetwork` Inspector 点击 `Assign Project Road Network Settings`。

### 2.2 打开制作窗口

推荐使用统一制作窗口：

- 菜单：`Tools > Blueprint System > Vehicle Road > Scene Authoring Tool`
- 或在 `RoadLaneNetwork` Inspector 点击 `Open Scene Authoring Tool`

窗口名为 `Road Lane Authoring`。先绑定目标 `Network`，再启用：

- `Scene Tool Active`
- `Live Network Preview`
- 按需要启用 `Snap To Road Colliders`
- 按需要启用 `Endpoint Snap` 和 `Auto Junction`

Scene View 中使用快捷键切换操作：

- `1` Select：只选择对象和 Knot，不创建、不绑定、不改拓扑。
- `2` Draw Lane：绘制普通 Lane。
- `3` Junction：绑定路口端点并生成 Connector。
- `4` Spline Edit：编辑 Lane/Connector 的 Knot。
- `5` Profile：编辑 Profile Source 控制点和拓扑断点。
- `6` Polygon：编辑开放区域 `RoadPolygonZone` 和边界 `RoadPortal`。

## 3. 制作 Lane

### 3.1 手绘 Lane

1. 打开 `Road Lane Authoring`。
2. 选择 `Draw Lane`。
3. 在 Scene View 点击放置 Knot，完成一条开放 Spline。
4. 生成对象会带 `SplineContainer + RoadLane`。
5. 在 `RoadLane` Inspector 检查：
   - `Lane Id`：稳定且唯一。
   - `Travel Direction`：`Forward`、`Reverse` 或 `Bidirectional`。
   - `Speed Limit`：运行时限速。
   - `Width`：默认 3.5m，可用 Width Keys 做沿程变化。
   - `Tag Mask`：道路标签。
   - `Allowed Agents`：允许的 Agent 类型。
   - `Allow Lane Change Left/Right`：换道权限。
   - `Open`：是否开放。
   - `Connection Mode`：`Automatic`、`Manual` 或 `Blocked`。

双向 Lane 在 Bake 后会生成两个稳定 ID 的有向 Lane。

### 3.2 端点吸附与自动拓扑

启用 `Endpoint Snap` 后，Draw Lane 完成或 Spline Edit 松开端点时会尝试：

- 吸附到兼容端点并创建 Automatic Connection。
- 在转向、分支或多端点汇聚时创建或复用 `RoadJunction`。
- 命中普通 Lane 中段时拆分原 Lane。
- 命中 Profile 管理 Lane 时回写到参考 Spline 控制点。

自动拓扑完成后建议马上：

1. 点击 `Validate Network`。
2. 检查 Scene View 预览的连接和相邻关系。
3. 确认需要保留人工修改的 Connector 已锁定。

### 3.3 快速复制相邻车道

相邻车道相关工具在 `Road Lane Authoring` 中维护。常规流程：

1. 选中已有 `RoadLane`。
2. 使用相邻复制工具生成左/右车道。
3. 检查复制后 Lane 的 `Lane Id`、方向、宽度、换道权限和连接模式。
4. 打开 `Preview Adjacent Links` 查看换道链接。
5. 打开 `Show Adjacent Inference Area` 查看相邻判断区域。
6. Bake 前再次 `Validate Network`。

左/右语义按车辆行驶方向判断，不按 Spline 绘制方向判断。

## 4. 制作 Junction 和 Connector

### 4.1 创建或绑定 Junction

常规流程：

1. 在 `Road Lane Authoring` 切到 `Junction`。
2. 选择需要汇入路口的 Lane 端点。
3. 创建或复用 `RoadJunction`。
4. 点击 `Refresh All Junction Connectors` 生成未锁定 Connector。
5. 检查红色/禁止转向的预览，调整 `Allowed Turns`。

`RoadJunction` 常用字段：

- `Junction Id`：稳定且唯一。
- `Allowed Turns`：允许直行、左转、右转或掉头。
- `Connector Base Cost`：Connector 路径成本。
- `Connector Speed Limit`：Connector 限速。
- `Traffic Control Mode`：路口控制模式。
- `Default Stop Line Distance`：停止线距离。
- `Queue Spacing`：队列间距。
- `Approach Detection Distance`：接近检测距离。
- `Signal Phases`：固定信号灯相位。
- `Bindings`：绑定到路口的 Lane 端点。

### 4.2 交通控制

车辆是否停车、排队、等待信号，运行时由 `VehicleRoadSubsystem` 的交通层决定。
`VehicleLaneFollower.LastOutput` 中最关键的字段是：

- `stopReason`
- `passageStatus`
- `signalState`
- `hasStopPoint`
- `stopPoint`
- `distanceToStopLine`
- `queueIndex`
- `junctionId`
- `connectorLaneId`

如果改了 `Default Stop Line Distance`、信号相位或队列参数，必须重新 `Bake Network Asset`。
运行时读取的是烘焙后的交通控制记录，不是场景组件上的最新 Inspector 值。

### 4.3 Blueprint 和 Behavior Tree 运行时节点

VehicleRoads 节点只覆盖运行时，不包含道路制作、Validate、Bake 或编辑器自动化。

普通 Blueprint 节点位于 `VehicleRoad.*`：

- `VehicleRoad.FindNearestLane`：从 `Binding<VehicleRoadSubsystem>`、世界位置、朝向和 `RoadAgentMask` 查询最近开放 Lane，输出 lane id、位姿和距离。
- `VehicleRoad.FindLaneRoute`：从起点/终点 Lane ID 查询同一 `BakedLaneNetwork` 内的 `Array<string>` 路线和总成本。
- `VehicleRoad.SetLaneClosed`：运行时关闭或重新开放 Lane。
- `VehicleRoad.UpdateVehicle` / `VehicleRoad.UnregisterVehicle`：向交通层发布或移除车辆状态。
- `VehicleRoad.EvaluateTrafficControl`：输出停车原因、信号、队列、停止点、前车和换道约束。
- `VehicleRoad.RequestLaneChange` / `VehicleRoad.CompleteLaneChange`：申请和完成相邻车道预约。
- `VehicleRoad.SetFollowerRoute` / `VehicleRoad.ComputeFollowerControl`：设置 `VehicleLaneFollower` 路线并计算转向、目标速度、恢复、停止和换道输出。节点不移动对象。
- `VehicleRoad.GetSubsystemSnapshot`：读取扁平诊断计数，适合 HUD、日志和测试断言。

这些 Blueprint 节点只通过 binding 名或连接的运行时对象解析 `VehicleRoadSubsystem`、`VehicleLaneFollower` 等组件；`.blueprint.json` 中不要存 Unity 对象引用。带副作用的节点都是 exec 节点，并缓存最后一次执行结果供输出端口读取。

Behavior Tree 节点位于 `BT.VehicleRoad.*`。查询/控制输出节点只写 Blackboard，不直接移动对象；`BT.VehicleRoad.DriveFollower` 是专门给 demo/AI 车辆使用的 kinematic 封装节点，会消费 `VehicleLaneFollower` 输出并移动 owner `Transform`。需要自定义流程时，可以改用函数级拆分节点组合：

- `BT.VehicleRoad.FindNearestLane`：写 `foundKey`、`laneIdKey`、pose 和距离 key，找到 Lane 才返回 `Success`。
- `BT.VehicleRoad.FindLaneRoute`：写 `successKey`、`routeLaneIdsKey: Array<string>` 和 `totalCostKey`，路线存在才返回 `Success`。
- `BT.VehicleRoad.SetFollowerRoute` / `SelectNextRouteTarget`：把 `Array<string>` 路线写入 `VehicleLaneFollower`，或从候选目的 Lane 中按 `First` / `Cycle` / `Random` 找到可达路线并写 `destinationLaneIdKey`、`selectedIndexKey`、`routeLaneIdsKey` 和 `totalCostKey`。
- `BT.VehicleRoad.ComputeFollowerControl`：从 Blackboard 或 owner GameObject 解析 `VehicleLaneFollower`，写 steering/speed/stop/lane-change 输出，`output.valid` 为真才返回 `Success`。
- `BT.VehicleRoad.DriveFollower`：便捷封装节点；从 Blackboard 或 owner GameObject 解析 `VehicleLaneFollower`，维护内部当前速度，按 `VehicleRoadTestVehicle` 的加减速、停止点夹紧、baked route pose 和 loop reset 逻辑移动 owner，额外写 `currentSpeedKey`、`arrivedKey` 和 `loopResetKey`。
- `BT.VehicleRoad.UpdateTrafficState` / `DecideLaneChange` / `RequestLaneChange` / `CompleteLaneChange`：发布车辆交通状态、读取前车信息、根据前车/停止点/恢复状态生成换道请求，并显式请求或完成换道。
- `BT.VehicleRoad.UpdateFollowerSpeed` / `EvaluateStopPointTravel` / `ApplyStopPoint`：拆分 `VehicleRoadTestVehicle.Update` 中的速度更新、停止点行驶距离计算和停止点吸附。
- `BT.VehicleRoad.CheckFollowerRouteEnd` / `MoveAlongBakedRoute` / `MoveTowardLookAhead`：拆分 baked route 终点判断、baked pose 移动和 look-ahead fallback 移动。
- `BT.VehicleRoad.CaptureLoopStart` / `TickLoopReset` / `UnregisterVehicle`：拆分 loop 起点捕获、延迟复位和车辆注销清理；`UnregisterVehicle` 可直接解析 `subsystem`，也可通过 follower 的 `RoadSubsystem` 解析。
- `BT.VehicleRoad.UpdateRoadAgent`：作为 service 周期性评估 `RoadAgent`，写目标点、恢复点、到达、失败原因和路线状态 key。

BT 节点的目标 key 是显式字符串属性，例如 `laneIdKey`、`targetSpeedKey`、`routeLaneIdsKey`。复杂结果对象不会写入 Blackboard；只写稳定的 primitive、Vector、`Array<string>` 和 enum 字段。推荐自定义车辆树使用 `SelectNextRouteTarget -> SetFollowerRoute -> ComputeFollowerControl -> UpdateTrafficState -> DecideLaneChange -> UpdateFollowerSpeed -> EvaluateStopPointTravel -> ApplyStopPoint/MoveAlongBakedRoute/MoveTowardLookAhead`，loop 分支配合 `CaptureLoopStart` 和 `TickLoopReset`。

## 5. 使用 Lane Profile 批量生成车道

当道路有固定横断面，例如双向四车道、公交车道或自行车道时，优先使用 Profile。

流程：

1. 在 `RoadLaneNetwork` 子层级创建带 `SplineContainer` 的对象。
2. 添加 `Vehicle Road/Road Network/Lane Profile Source`。
3. 设置稳定 `Source Id`。
4. 创建或指定 `RoadLaneProfile`。
5. 给每个 Profile Entry 设置稳定 `Entry Id`、宽度、方向、限速、标签、Agent 和换道权限。
6. 选择 `Alignment`：`Center`、`LeftEdge` 或 `RightEdge`。
7. 点击 `Apply Lane Profile`。
8. 在 `Profile` 模式下按 Knot 设置 Profile Override 或 `Force Topology Break`。
9. `Validate Network` 后 `Bake Network Asset`。

规则：

- Entry ID 稳定，Bake 后 Lane ID 也会稳定。
- 未锁定受管 Lane 会被 Profile 刷新。
- 锁定 Lane 保留人工修改。
- 删除 Entry 后对应 Lane 变为 Orphaned，不会自动删除。
- 需要在某个 Knot 强制断开拓扑时使用 `Force Topology Break`。

## 6. 使用 Polygon Zone 和 Portal

Polygon 用于开放区域、广场、停车场、人车混合空间或非 Lane 路线段。

推荐在 `Road Lane Authoring` 切到 `Polygon` 模式：

1. 左键放置 Polygon 草稿顶点。
2. `Enter` 或右键创建 `RoadPolygonZone`。
3. 拖动顶点调整边界，点击边中点插入新顶点。
4. 选中顶点后按 `Delete` 删除；少于 3 个顶点时不会继续删除。
5. 使用高度手柄调整 `Minimum Height` 和 `Height`，Scene View 会显示底面、顶面和垂直边。
6. 打开 `Create Portal On Boundary`，在 Active Zone 边界点击创建 `RoadPortal`。
7. 拖动 Portal 时会自动投影到最近 Polygon 边界，并按边界切线对齐宽度条。
8. 查看最近 Lane 端点或其他 Polygon Portal 的建议目标，确认后点击 `Apply Suggested Target`。
9. `Validate Network`。
10. `Bake Network Asset`。

注意：

- Polygon 支持凸形和凹形，但不支持自交、多环和洞。
- Portal 必须在有效边界上，并且宽度、方向和目标连接要合法。
- Portal 只会显示建议目标，不会在创建或拖动时自动写入 `Linked Lane` 或 `Linked Portal`。
- Lane 建议会显示端点，并允许手动选择是否使用反向运行时 Lane；Forward 默认 false，
  Reverse 默认 true，Bidirectional 默认 false。
- Polygon 段不应用车辆信号、队列和换道规则；这些只在 Lane/Connector 段生效。

## 7. Bake 与验证

每次完成编辑后执行：

1. 在 `Road Lane Authoring` 或 `RoadLaneNetwork` Inspector 点击 `Validate Network`。
2. 处理所有阻断错误。
3. 点击 `Bake Network Asset`。
4. 确认生成或原位更新的 `BakedLaneNetwork.asset`。
5. 确认 Schema 为 `3.1`，Summary 里的 invalid 计数为 0。

Bake 会把编辑态数据写入 ScriptableObject：

- Lane/Connector 采样。
- 左右边界、宽度、姿态、曲率和累计距离。
- 纵向连接。
- 相邻换道链接。
- Junction 交通控制记录。
- Polygon、三角形和 Portal。
- 空间索引和烘焙摘要。

不要手动编辑 `BakedLaneNetwork.asset` 的内部数据。

## 8. 运行时接入

### 8.1 场景放置 VehicleRoadSubsystem

1. 在场景中创建对象，例如 `VehicleRoadSubsystem`。
2. 添加 `Vehicle Road/Vehicle Road Subsystem`。
3. 在 `Networks` 中添加一个或多个 `BakedLaneNetwork.asset`。
4. 或启用 `Auto Register Scene Road Lane Networks`，让它从场景里的 `RoadLaneNetwork`
   自动发现烘焙资产。
5. 绑定 `Runtime Settings`。
6. 如果设置未绑定，可在 Inspector 点击 `Assign Project Runtime Diagnostics Settings`。

`VehicleRoadSubsystem` 是显式场景组件，不是全局单例。需要道路查询、车辆交通控制、诊断快照
的对象应明确引用它。

### 8.2 接入 VehicleLaneFollower

车辆对象上添加 `Vehicle Road/Vehicle Lane Follower`。

两种接入方式：

- 直接绑定 `Lane Network`：只使用单个 `BakedLaneNetwork` 的路线/跟随能力。
- 绑定 `Road Subsystem`：使用统一查询、交通控制、队列、信号和换道能力。

如果要让红灯、停车线、队列和换道生效，必须绑定 `Road Subsystem`，并且调用
`ComputeControl` 时传入稳定 `vehicleId`。

执行器每帧应提供：

- 车辆位置。
- 车辆朝向。
- 当前速度。
- 轴距和车长。
- 车辆类型。
- 前车距离和速度。
- 是否请求换道及换道方向。

然后消费 `VehicleLaneFollowerOutput`：

- `targetSteeringAngle`
- `targetSpeed`
- `lookAheadPoint`
- `recoveryMode`
- `recoveryPosition`
- `recoveryRotation`
- `stopReason`
- `stopPoint`
- `distanceToStopLine`
- `signalState`
- `queueIndex`
- `laneChangeStatus`

`VehicleLaneFollower` 只输出控制意图，不负责 Rigidbody、轮胎、碰撞、局部避障或最终位移。

### 8.3 使用 RoadAgent

`RoadAgent` 是通用路线状态机，适合非车辆或需要 Lane/Polygon 混合路线的对象。

1. 添加 `Vehicle Road/Road Network/Road Agent`。
2. 设置稳定 `Agent Id`。
3. 绑定 `Road Subsystem`，或设置 `Fallback Network`。
4. 配置 `RoadAgentProfile`。
5. 调用 `SetDestination(currentPosition, worldDestination)`。
6. 每帧调用 `Evaluate(currentPosition, currentForward, currentSpeed, deltaTime)`。
7. 外部执行器消费 `LastOutput` 或返回输出。

`RoadAgent` 不移动 Transform，不依赖 Rigidbody、CharacterController 或 NavMeshAgent。

## 9. 运行调试窗口

菜单：

`Tools > Blueprint System > Vehicle Road > Road Network Runtime Debug`

可用于：

- 指定 `VehicleRoadSubsystem` 或单个 `BakedLaneNetwork`。
- 设置 `Agent Mask`、`Agent Radius` 和 Tag Filter。
- 执行 `Nearest Element` 查询。
- 执行 `Point / Sphere / Bounds` 查询。
- 执行路线查询。
- 查看网络、Agent、队列、Token、查询/路线计数和历史占用。
- 按稳定 Agent ID 获取 Agent 快照。
- 复制有行数上限的紧凑报告。

建议只在需要定位问题时开启详细历史。Release Build 中详细 Profiler 和历史会关闭。

## 10. 常见问题排查

### 10.1 改了路但运行时没变化

按顺序检查：

1. 是否点击了 `Bake Network Asset`。
2. 车辆或 Subsystem 是否引用了刚刚更新的 `BakedLaneNetwork.asset`。
3. Play Mode 是否还在使用修改前的运行时注册数据。
4. 是否只是 `Live Network Preview` 变化，而正式资产没有 Bake。

### 10.2 车辆没有停在停止线前

先抓运行态事实，不要先改队列或停止线算法。

检查当前车的：

- `VehicleLaneFollower.LastOutput.stopPoint`
- `VehicleLaneFollower.LastOutput.distanceToStopLine`
- `VehicleLaneFollower.LastOutput.queueIndex`
- `VehicleLaneFollower.LastOutput.signalState`
- 车辆实际 Transform 位置。
- 是否绑定 `VehicleRoadSubsystem`。
- 是否传入稳定 `vehicleId`。

判断层级：

- `LastOutput` 里的停止点就是错的：回到 Bake、Subsystem 或交通控制记录。
- `LastOutput` 正确但车辆没停到位：检查车辆执行器。
- Demo 车看起来越过停止线：注意 `VehicleRoadTestVehicle` 是示例执行器，它按控制点/中心点夹紧，
  不是正式前保险杠物理模型。

### 10.3 查询找错层或找不到 Lane

检查：

- `Agent Mask` 是否允许。
- `Agent Radius` 是否超过 Lane 有效宽度。
- Tag Filter 的 `all/any/none` 是否过严。
- 最大高度差是否过小。
- Lane 是否 `Open`。
- 起终点是否在同一个 `BakedLaneNetwork` 内。

### 10.4 换道不可用

检查：

- 是否存在 Adjacent Link。
- `Allow Lane Change Left/Right` 是否开启。
- 左右方向是否按车辆行驶方向理解。
- 目标 Lane 是否开放。
- 目标 Lane 是否允许当前 Agent。
- `VehicleRoadSubsystem` 是否有安全窗口或预约冲突。
- Follower 输出中的 `laneChangeStatus` 和 `laneChangeTargetLaneId`。

### 10.5 Connector 没刷新或转向不对

检查：

- Lane 端点是否正确绑定到 `RoadJunction`。
- `Allowed Turns` 是否允许该转向。
- Connector 是否被锁定。
- Connector 是否 Orphaned。
- 点击 `Refresh All Junction Connectors` 后是否重新 Bake。

## 11. 提交或交付前检查清单

- `Validate Network` 无阻断错误。
- `Bake Network Asset` 已执行。
- `BakedLaneNetwork` Schema 为 `3.1`。
- Summary invalid Lane/Polygon/Sample 数量为 0。
- 关键 Lane、Junction、Profile Entry、Polygon、Portal ID 稳定且唯一。
- 需要运行时交通规则的车辆已绑定 `VehicleRoadSubsystem`。
- 停车线、信号和队列改动已经通过 Follower 输出验证。
- 相邻换道通过 Adjacent Preview 和运行时 `laneChangeStatus` 验证。
- 没有把 Vehicle Roads 逻辑接到 NavMesh。
- 没有手动修改烘焙资产内部数据。
