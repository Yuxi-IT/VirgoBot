let ws;
let username = "0x536D61";

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function sendTextMsg(text) {
    const messages = text.split(/\r?\n\r?\n/);
    for (const msg of messages) {
        await sleep(150);
        const input = document.querySelector("div[class='ace-line']");
        input.innerText = msg;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        await sleep(150);
        document.querySelector(".e2e-send-msg-btn").dispatchEvent(
            new MouseEvent("click", { bubbles: true, cancelable: true, view: window })
        );
    }
}

function connectWebSocket() {
    ws = new WebSocket("ws://localhost:5000/");

    ws.onopen = () => {
        console.log("WebSocket 已连接");
    };

    ws.onmessage = async (event) => {
        const data = JSON.parse(event.data);

        if (data.type === "reply") {
            await sendTextMsg(data.content);
        } else if (data.type === "email") {
            console.log("收到邮件通知:", data.subject);
            await sendTextMsg(`📧 ${data.message}`);
        } else if (data.type === "proactive") {
            console.log("AI主动消息:", data.content);
            await sendTextMsg(data.content);
        }
    };

    ws.onerror = (err) => console.error("WebSocket错误:", err);
    ws.onclose = () => {
        console.log("WebSocket断开，3秒后重连");
        setTimeout(connectWebSocket, 3000);
    };
}

let lastMsg = "";

async function startMessageListener() {
    console.log("聊天监听服务已启动");

    setInterval(async () => {
        const msg = document.querySelector(".messageMessageBoxcontentBox:not(.messageMessageBoxisFromMe)");
        const userName = document.querySelector(".RightPanelHeadertitle");

        if (!msg) return;

        if (userName.innerText.lastIndexOf("(") != -1) {
            const fromName = document.querySelector(".MessageBoxMessageTitleavatarName");
            if (fromName.innerText !== username) return;
        }

        await sleep(250);

        if (userName.innerText == username && msg.innerText !== lastMsg) {
            console.log("收到新消息:", msg.innerText);
            lastMsg = msg.innerText;

            if (ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    message: msg.innerText,
                    userId: userName.innerText
                }));
            }
        }
    }, 1000);
}

connectWebSocket();
startMessageListener();
