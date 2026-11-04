# 问题排查

## 菜单未出现或编译报错

先确认安装的是 `main`，并且项目没有同时导入旧版本或 `ecs` 分支。`main` 的运行时不引用 PureMVCFramework；若仍报相关类型缺失，检查项目内是否残留旧源码。使用 asmdef 的业务程序集需要引用 `GpuAnimator`，Editor 工具由独立的 `GpuAnimator.Editor` 程序集编译。

包声明的最低 Unity 版本是 2020.2。附带 Shader 使用 Built-in Render Pipeline；URP/HDRP 出现粉色材质时，需要移植对应 Shader，不能只更换播放脚本解决。

## 烘焙数量为零或没有输出

按[快速开始](getting-started.md#准备原始资源)核对完整目录结构。菜单扫描的是固定路径 `Assets/Artworks/GpuRole/`，不是 Project 窗口当前选择。

当前工具要求 `<Role>.fbx`、`res/<Role>.png`、`res/<Role>.mat` 和至少一个小写扩展名 `.anim`。FBX 内嵌动画不会由此菜单自动枚举。SMR 节点名称必须包含角色目录/FBX 名，且区分大小写；`rootBone` 需要有效。

不满足文件检查的角色会被跳过。可查看 Editor 工具的 `GpuSkinningTools.errors` 检查结果；当前菜单没有自动输出这份错误列表文件。先准备一个符合约定的角色，再批量处理。

## 多个部件只生成一个，或材质结果与原模型不同

当前生成器实际只复制一个被选中的 SMR；多个名称匹配时取最后一个，不会合并其余身体/装备。建议先将本次要生成的角色整理为单 SMR、单 submesh 输入。单个 SMR 的 `subMeshCount > 1` 时生成器会记录错误；输出也只配置生成材质，没有通用多材质保留流程。

输入 `.mat` 是当前文件检查的必需项，但工具会重新创建 GPU 材质，不会复制其全部设置。主贴图、可选遮罩和动画纹理由工具绑定，其余材质属性需要项目配置。生成 prefab 默认关闭投射和接收阴影。

## 新增的输出资源在重新烘焙后消失

工具会删除并重建 `Assets/Main/Prefabs/GpuRole/<Role>/`。把手工维护的资源放到其他目录，再引用生成文件；不要把此输出目录当作手工作业目录。执行菜单会处理扫描到的全部角色。

## Initialize 或 TryPlay 返回 false

| 结果 | 检查内容 |
| --- | --- |
| `Initialize` 返回 `false` | Renderer 是否存在；元数据及 `clips` 是否非空；`totalFrame` 是否大于零。失败后旧绑定已被 Clear |
| `TryPlay` 返回 `false` | 是否已初始化；名称是否与 `clip.name` 精确一致；帧区间是否在 `[0, totalFrame - 1]`；帧率是否有限且大于零 |
| `FromBytes` 抛异常 | 是否加载了对应的 `_Data.bytes`，而不是 PNG、普通文本或其他对象；数据是否来自兼容的烘焙版本 |

Clip 名称来自 `AnimationClip.name`，不一定等于文件名。遍历 `sharedData.clips` 检查实际名称。空项会被跳过；重复名称会匹配第一项。`TryPlay` 失败不会停止旧动画，`Play` 则没有返回结果，排查时优先用 TryPlay。

## 初始化成功但不动，或动作不对

初始化不会自动播放，请显式调用 TryPlay。生成 prefab 虽然已经带 GpuAnimator，却仍需要调用方解码并注入元数据。

必须成套使用同次烘焙产生的 Mesh、材质、动画 PNG 与 `.bytes`。即使 Initialize 返回 true，它也没有验证资源是否配对。Renderer 上应使用 GPU 动画 Shader；材质的 `_AnimationTex`、`_BoneNum`、`_AnimationTexSize` 要对应生成数据。不要把主贴图 `<Role>.png` 当成动画纹理 `<Role>_Data.png`。

检查组件是否启用、对象是否激活、`AutomaticUpdate` 是否为 true；手动模式需要外部持续调用 Tick。若每帧重新调用 TryPlay，计时会不断清零，动画会停留在开头。播放循环动作时让业务逻辑只在状态改变时切换。

## 播放过快、复用后停住或起点不一致

同时启用自动 Update 和外部 Tick 会重复推进。统一管理时将每个实例的 `AutomaticUpdate` 设为 false；恢复自动模式时显式设回 true。Clear/Initialize 不会重置这个属性。

`TryPlay` 默认从第一帧开始；便捷方法 `Play` 默认随机起始相位。需要确定起点时使用 `TryPlay(name)` 或显式 `randomizeStart: false`。随机不是分配独立动画纹理，每个实例仍共享相同资源。

## 完成回调没有触发或切换时旧回调消失

只有非循环动画自然结束才会调用一次完成回调。`loop: null` 使用烘焙设置，强制一次性播放可传 `loop: false`。从第一帧开始，长度为 N 的动画在逻辑上经过 N 个采样间隔后完成；正常逐采样推进时包含末帧的间隔，较大的 Tick 则可能跳过采样帧，直接跨越完成边界。

跨越完成边界时，末帧提交和回调执行发生在同一次 Tick 中。回调若立即 TryPlay，可能在屏幕渲染前覆盖末帧，所以回调触发不保证末帧已经实际显示，更不保证显示了一个完整间隔。

Stop、Clear、重新 Initialize 或成功启动另一段动画都会取消旧的待执行回调，不会把取消视作自然完成。`TryPlay` 失败则保留原播放及回调。组件停用只会停止自动 Update；项目自己的异步任务和外部 Tick 仍应由适配层管理。

## 自定义实例颜色被下一帧覆盖

播放器缓存并回写整份 PropertyBlock。如果其他代码修改 Renderer 的 PropertyBlock，请在修改后调用 `player.SetPropertyBlock()`。同时保留块中的其他属性，让 `_FrameIndex` 与 `_BlendFrameIndex` 由播放器控制，详见 [PropertyBlock 协作](integration.md#propertyblock-协作)。

## 动画在镜头边缘突然消失

当前生成器克隆原 Mesh 的静态 bounds，没有计算所有 GPU 动画姿态的包围盒并集。MeshRenderer 在 CPU 端按静态 bounds 做剔除，不会知道 Shader 将顶点移到哪里；跳跃、根位移等使顶点离开原始范围时可能提前消失。

需要在项目中为生成 Mesh 配置覆盖全部动作的保守 bounds，或扩展烘焙工具计算动画 bounds。只调用 `RecalculateBounds()` 仍只会看到 CPU 中未蒙皮的顶点。共享 Mesh 的 bounds 修改会影响所有使用它的实例。

## 缩放、扭转或过渡与期望不同

格式保存每骨骼一个统一缩放值，骨骼 X/Y/Z 应一致；这属于数据设计约束。非均匀缩放、剪切不能被旋转加统一缩放精确表达。蒙皮采用最多四权重 LBS，关节大角度扭转时不提供 DQS 的体积保持效果。

当前 Shader 不执行帧间插值和 Clip 过渡混合；`_BlendFrameIndex` / `_BlendProgress` 属性的存在不表示已经启用交叉淡化。需要更平滑的时间采样时，可以提高输入 Clip 的烘焙帧率；需要真正的插值/混合则需扩展实现。

升级后重新烘焙旧动画 PNG。旧版对缩放向量取 magnitude 并截断到 `[0,1]`，已经丢失的数据不能仅靠新解码器修复。若不同 Clip 在缺少某些骨骼轨道时出现顺序相关的姿态，检查输入动画是否完整：当前工具连续在同一对象上 SampleAnimation，没有为每个 Clip 恢复基准姿态。

## 动画纹理导入后数据损坏

生成器把数值编码进 RGBA 字节，因此动画 PNG 应保持：

- Point 过滤、关闭 mipmap。
- 关闭 sRGB，使用无压缩 RGBA32，保留 alpha。
- 保留生成尺寸，不进行缩放或有损平台压缩。

工具已设置上述过滤、mipmap、sRGB 和无压缩选项，但当前代码没有设置 `maxTextureSize` 或各平台覆盖。若动画纹理超过导入器允许的尺寸，Unity 可能缩小它，破坏像素寻址；检查实际导入尺寸是否仍与材质 `_AnimationTexSize`、元数据 `texWidth/texHeight` 一致，并结合目标平台允许的最大纹理尺寸配置导入设置。

默认的当前烘焙路径每骨骼每帧使用 8 个 RGBA32 像素。增加骨骼数、Clip 数或烘焙帧率会增加纹理大小；运行时的 Tick 跳帧处理不会降低烘焙资源的存储量。
