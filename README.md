## 0 coding 纯vibe
本项目完全由codex开发，全程聊天修改逻辑，一天run起来。最下面有直播录像

https://github.com/user-attachments/assets/62f93228-8809-45c3-a432-a451aba46220

全程0coding，一天完成，这在之前几乎是不可能完成的，哥们不吹牛逼，也是985十年大厂资深程序了，  
之前自学游戏引擎unity ue什么的，真的，想要实现类似的逻辑，真的要花好久好久好久，  
理解底层机制，逻辑编写，ui布局才能上手，起码几个月出去了，这个直接一天搞定，唯有震撼。

## 训练AI打牌（请注意，这部分ai训练流程代码，包括下述训练ai的指引文档也是ai生成的）


https://github.com/user-attachments/assets/4c6d7eaa-08fb-4abe-99ff-614df744f8b4


如果你是刚 clone 下来，先看训练和运行说明：

- 训练流程文档: [Docs/TrainingGuide.md](Docs/TrainingGuide.md)
- Python 控制层说明: [Tools/python/README.md](Tools/python/README.md)
- MCP / Agent 说明: [Tools/AGENT_CONTROL.md](Tools/AGENT_CONTROL.md)

最短跑通路径：

1. 安装 Python 3、.NET SDK、Godot Mono
2. 在项目根目录执行 `powershell -ExecutionPolicy Bypass -File .\build.ps1`
3. 用 Godot 运行游戏
4. 采样: `powershell -ExecutionPolicy Bypass -File .\Tools\run-rollout.ps1`
5. 训练: `powershell -ExecutionPolicy Bypass -File .\Tools\run-training-stage1.ps1 -Replay`
6. 评估: `powershell -ExecutionPolicy Bypass -File .\Tools\run-eval.ps1 -Policy tabular`

注意：

- 训练、评估、bot 控制前，必须先把游戏运行起来；Python 脚本会连接本地 bridge `127.0.0.1:47077`
- 如果新开的 PowerShell 里 `python --version` 失败，可以先设置 `SLAY_THE_HS_PYTHON`

```powershell
$env:SLAY_THE_HS_PYTHON = 'C:\Path\To\python.exe'
```

常见报错：

- `ConnectionRefusedError` / 无法连接 `127.0.0.1:47077`：通常是游戏没开，或者 bridge 端口不是默认值
- `python` 找不到：重开 PowerShell 后再试，或者按上面设置 `SLAY_THE_HS_PYTHON`


## godot 4.5.1
因为godot场景等都是可以文本diff，ai友好，我愿称之为ai native game engine  
这实现的效果就是，我这个demo里面不止没有写代码！我连素材，资源，场景文件，也没有自己写！ai连场景文件，ui布局，都能直接给你写！

![BE06A99AC1DF9E807615C041B74057A7](https://github.com/user-attachments/assets/7e4d118b-c753-49b2-9fa4-6983eaa607c3)

## codex
基于git分支，每个agent自己开发一块功能，合理做好解耦，分配给他们不同模块的任务，实现类似，好几个人并发给你开发的效果。  
你就验证下他们的开发结果，然后再把满意的合并到master。就跟大公司多个开发基于git协调的ci工作流一样，效率倍倍倍倍增

codex使用下来，体验比claude不差，甚至我感觉更好，claude两天花了我30刀乐，都没搭出这效果的一半  
而且codex便宜大碗，搞完这个，甚至额度还挺富裕，不过我确实是准备开200刀的pro了，接下来搞个大的！

<img width="2568" height="1082" alt="image" src="https://github.com/user-attachments/assets/e020c109-2967-44fa-8a42-e605fd968278" />


## ai时代，唯有震撼，world changed...

【codex 多任务工作流太牛了，唯有震撼，全程高能-哔哩哔哩】 https://b23.tv/0Ps1VZx
