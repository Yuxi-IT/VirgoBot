let ws;
let username = "0x536D61";
let processing = false;

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
            console.log("收到新邮件:", data.subject);
            await sendTextMsg(`📧 新邮件\n发件人: ${data.from}\n主题: ${data.subject}\n\n${data.message}`);
        }

        processing = false;
    };

    ws.onerror = (error) => {
        console.error("WebSocket 错误:", error);
    };

    ws.onclose = () => {
        console.log("WebSocket 断开，3秒后重连...");
        setTimeout(connectWebSocket, 3000);
    };
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

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

let lastMsg = "";

function startMessageListener() {
    console.log("聊天监听服务已启动");
    setInterval(() => {
        const msg = document.querySelector(".messageMessageBoxcontentBox:not(.messageMessageBoxisFromMe)");
        const userName = document.querySelector(".RightPanelHeadertitle");

        if (!msg || !userName) return;

        if (userName.innerText.lastIndexOf("(") != -1) {
            const fromName = document.querySelector(".MessageBoxMessageTitleavatarName");
            if (fromName && fromName.innerText !== username) return;
        }

        if (userName.innerText === username && msg.innerText !== lastMsg && !processing) {
            console.log("收到新消息:", msg.innerText);
            lastMsg = msg.innerText;
            processing = true;

            ws.send(JSON.stringify({
                message: msg.innerText,
                userId: userName.innerText
            }));
        }
    }, 1000);
}

connectWebSocket();
startMessageListener();
