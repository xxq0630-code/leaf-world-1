# leaf codex-pet

一个保留冷淡、不爱笑气质的 Windows 像素桌宠。

![leaf codex-pet preview](pixel-sprite-preview.png)

## 下载运行

下载并双击 [`leaf-codex-pet.exe`](leaf-codex-pet.exe)。角色素材已嵌入程序，EXE 可以单独运行。

> 如果 Windows SmartScreen 提示未知发布者，这是因为程序由本地编译、没有商业代码签名。确认文件名后，可通过“更多信息”继续运行。

## 互动

- 左键单击：随机说一句冷面台词
- 左键拖动：移动桌宠
- 左键双击：确认他依旧没笑
- 右键：控制散步、暂停、说话频率、大小和退出

## 特点

- 80×160 原生像素精灵
- 48 色调色板
- 2 倍最近邻显示
- 像素眨眼、呼吸、微摆、鼠标方向响应和小范围散步
- 单文件 Windows EXE，无需额外安装运行库

## 文件

- `leaf-codex-pet.exe`：可直接运行的桌宠
- `leaf-codex-pet.zip`：完整下载包
- `LeafCodexPet.cs`：WPF/C# 源码
- `pixel-sprite-80x160.png`：原生像素角色素材
- `pixel-sprite-preview.png`：4 倍预览图

## 构建

项目使用 Windows 自带的 .NET Framework WPF 组件，可通过 `csc.exe` 编译。PNG 会以 `DeadpanPet.Character.png` 资源名嵌入 EXE。

