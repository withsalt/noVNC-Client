# noVNC-Client

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

基于 ASP.NET Core 的 noVNC 客户端，让你能轻松从 Web 浏览器访问 VNC 服务器，无需安装任何客户端软件。

## 🚀 快速开始

### 如何使用

1. **安装VNC服务端**  
这里以TightVNC为例，安装过程不再赘述（建议以服务方式运行）  
安装完成之后，按照下图勾选必要配置项目：

    ![TightVNC配置截图1](docs/images/screenshot_20251201143102.png)

    ![TightVNC配置截图2](docs/images/screenshot_20251201143116.png)

    ![TightVNC配置截图3](docs/images/screenshot_20251201143150.png)

1. **下载vnc client**
    ```bash
    git clone https://github.com/yourusername/noVNC-Client.git
    cd noVNC-Client
    ```

2. **配置 VNC 服务器连接（一般不用做任何更改）**

    编辑 `src/noVNCClient/appsettings.json` 文件：

    ```json
    {
    "Websockify": {
        "Path": "/websockify",
        "Host": "127.0.0.1",
        "Port": 5900
    }
    }
    ```

    参数说明：
    - `Path`: WebSocket 代理的路径（默认即可）
    - `Host`: VNC 服务器的 IP 地址或主机名
    - `Port`: VNC 服务器的端口号（默认 5900）

3. **运行应用**

    使用Https
    ```bash
    ./noVNCClient.exe --Kestrel:Endpoints:Https:Certificate:Path="./novnc.pfx" --Kestrel:Endpoints:Https:Certificate:Password="" --Kestrel:Endpoints:Https:Url="https://*:5909" 
    ```
    使用http
    ```bash
    ./noVNCClient.exe --urls="http://*:5909"
    ```

4. **访问应用**

    打开浏览器访问：
    - 完整版界面: `https://<目标机器IP地址>:5909` 或 `http://<目标机器IP地址>:5909`
    - 精简版界面: `https://<目标机器IP地址>:5909/Lite`


## 📝 更新 noVNC 版本

项目根目录提供了 `update-novnc.ps1` 脚本，可以自动更新 noVNC 到最新版本。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE)。

noVNC 组件采用 [MPL 2.0 许可证](src/noVNCClient/wwwroot/LICENSE.txt)。

## 🔗 相关链接

- [noVNC 官方网站](https://novnc.com/)
- [noVNC GitHub](https://github.com/novnc/noVNC)
- [ASP.NET Core 文档](https://docs.microsoft.com/aspnet/core/)
- [RFB 协议文档](https://github.com/rfbproto/rfbproto)

## 📧 联系方式

如有问题或建议，请提交 Issue 或联系项目维护者。

---

**注意**: 本项目仅用于学习和研究目的。在生产环境中使用时，请确保配置适当的安全措施，如 HTTPS、身份验证和访问控制。