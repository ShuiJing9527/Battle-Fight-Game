# 项目 BUG 扫描备忘录

扫描日期：2026-07-27  
扫描方式：静态代码与 Unity 场景序列化配置审查  
项目版本：Unity 6000.2.14f1

## 总结

本次确认：

- 高风险问题：3 个
- 中等风险问题：4 个
- 未执行 Unity 构建或 PlayMode 测试
- 未发现项目自有自动化测试

## 高风险问题

### 1. Player 构建可能编译失败

TOD 运行时程序集直接引用了 `UnityEditor`，但没有使用 `#if UNITY_EDITOR` 隔离。编辑器内可能正常编译，但 Player 构建时无法使用 `UnityEditor` 命名空间。

相关位置：

- `Packages/com.ahd2.tod-system/Runtime/TODGlobalParameters.cs:5`
- `Packages/com.ahd2.tod-system/Runtime/ReflectionProbe/ReflectorProbe.cs:6`
- `Packages/com.ahd2.tod-system/Runtime/AHD2.TODSystem.Runtime.asmdef`

建议：

- 删除未使用的 `using UnityEditor;`。
- 如果确实需要编辑器 API，使用 `#if UNITY_EDITOR` 包裹相关引用和代码。

### 2. 反射探针每帧泄漏 CommandBuffer

`ReflectorProbe.Update()` 每帧调用 `CommandBufferPool.Get()`，但执行完成后没有调用 `CommandBufferPool.Release()`。

主战斗场景中的该组件处于启用状态，因此可能持续产生内存和渲染资源压力。

相关位置：

- `Packages/com.ahd2.tod-system/Runtime/ReflectionProbe/ReflectorProbe.cs:122`
- `Packages/com.ahd2.tod-system/Runtime/ReflectionProbe/ReflectorProbe.cs:133`
- `Assets/Scenes/草原.unity:476315`

建议：

- 在所有执行路径结束前调用 `CommandBufferPool.Release(cmd)`。
- 同时检查 `cubemap`、`skyboxmap`、`skyboxmapmirror` 等 GPU 资源是否在销毁时正确释放。

### 3. 玩家生成失败可能永久卡住加载画面

玩家生成存在以下链路：

1. TOD 未准备好时，生成方法排队重试并返回 `true`。
2. 角色配置错误或找不到安全出生点时，同样返回 `true`。
3. 地图生成器把 `true` 当作生成已处理，因此跳过后备生成逻辑。
4. TOD 重试最多持续 30 帧，超时后直接取消玩家生成。
5. 加载界面无限等待 `Player01` 和 `Player02`，没有超时或降级处理。

最终可能导致玩家没有生成，加载遮罩永久停留。

相关位置：

- `Assets/Script/角色/PlayerSpawnManager.cs:66`
- `Assets/Script/角色/PlayerSpawnManager.cs:86`
- `Assets/Script/角色/PlayerSpawnManager.cs:128`
- `Assets/Script/角色/PlayerSpawnManager.cs:480`
- `Assets/Script/随机地图/RandomMapGeneration.cs:705`
- `Assets/Scripts/SceneFlow/BattleSceneLoadingGate.cs:109`

建议：

- 只有实际完成生成时才返回成功。
- 重试超时后执行明确的后备出生逻辑。
- 为加载门增加超时、错误提示和安全降级。
- 避免通过硬编码对象名称判断核心对象是否就绪。

## 中等风险问题

### 4. 主菜单加载界面未绑定

开始按钮已经绑定 `SimpleLoadBar.OnClickStartGame()`，但加载界面的进度条、文字、根节点和黑色遮罩引用全部为空。

场景仍可能继续加载，但用户看不到加载进度或遮罩反馈。

相关位置：

- `Assets/Zhu/GameScene.unity:1029`
- `Assets/Zhu/GameScene.unity:1030`
- `Assets/Zhu/GameScene.unity:1033`
- `Assets/Zhu/GameScene.unity:3687`

建议：

- 在 Inspector 中补齐四个 UI 引用。
- 增加启动时配置检查，缺少必要引用时输出明确错误。

### 5. 玩家受伤和死亡音效监视器失效

`HealthAudioWatcher` 默认查找名为 `currentHP` 的字段，但玩家实际血量字段为 `CombatHealth.currentHealth`。

两个玩家预制体仍配置为 `currentHP`，并且受伤、死亡音效资源均未绑定。

相关位置：

- `Assets/Zhu/SFX/HealthAudioWatcher.cs:10`
- `Assets/shukaisho/CombatHealth.cs:10`
- `Assets/Prefabs/Player/Player01.prefab:1742`
- `Assets/Prefabs/Player/Player02.prefab:1684`

建议：

- 将字段名改为 `currentHealth`，或让监视器直接依赖 `CombatHealth`。
- 为两个玩家预制体绑定受伤和死亡音效。
- 长期建议使用事件通知代替反射读取字段。

### 6. Player02 队长配置可能被忽略

初始化代码可以正确识别 `partyLeader`，但只要 `player01` 存在，起始玩家就会优先选择 Player01。

因此 Inspector 中配置 Player02 为队长时，静态或后备初始化路径仍可能从 Player01 开始。

相关位置：

- `Assets/Script/角色/Player2Bootstrap.cs:745`
- `Assets/Script/角色/Player2Bootstrap.cs:762`

建议：

- 起始玩家应优先使用有效的 `partyLeader`。
- 仅在队长为空时回退到 Player01 或 Player02。

### 7. 胜利判定存在未完成逻辑

`ArmFinalRushVictory()` 的名称表示启用最终冲刺胜利条件，但方法内部把 `finalRushVictoryArmed` 设置为 `false`。

该字段没有被读取，剩余敌人数检查也只输出日志，不会调用胜利逻辑。正常的清场 Boss 死亡路径可以触发胜利，但 Boss 生成或绑定异常时没有后备判定。

相关位置：

- `Assets/Scripts/Enemies/Core/EnemyDifficultyDirector.cs:103`
- `Assets/Scripts/Enemies/Core/EnemyDifficultyDirector.cs:430`
- `Assets/Scripts/Enemies/Core/EnemyDifficultyDirector.cs:462`

建议：

- 明确最终胜利规则，删除废弃状态或补齐读取逻辑。
- 修正 `ArmFinalRushVictory()` 的赋值。
- 在剩余敌人为零且满足阶段条件时提供可靠的后备胜利路径。

## 关于 InvalidCastException

此前提供的异常堆栈全部位于 Unity 编辑器的脚本编译管线内部：

`UnityEditor.Scripting.ScriptCompilation.EditorCompilation.CompleteActiveBuildWhilePumping`

单凭该堆栈无法确认具体项目脚本是直接根因。建议优先修复运行时程序集引用 `UnityEditor` 的问题，然后执行以下检查：

1. 关闭 Unity。
2. 备份项目。
3. 删除或重命名 `Library` 目录。
4. 重新打开项目并等待完整导入。
5. 检查 Console 中最早出现的编译错误，而不是最后出现的编辑器内部异常。

## 后续处理顺序

1. 修复 TOD 运行时程序集的 `UnityEditor` 引用。
2. 修复 `ReflectorProbe` 的 CommandBuffer 和 GPU 资源释放。
3. 修复玩家生成返回值、超时和加载门降级机制。
4. 补齐主菜单加载 UI 引用。
5. 修复血量音效字段和资源配置。
6. 修正队长初始化与最终胜利逻辑。
7. 执行 Player 构建、PlayMode 测试和长时间性能测试。
