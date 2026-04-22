let ws;
let username = "SmaZone";
let monitoredChats = new Set();
let messageQueue = [];

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function sendTextMsg(text) {
    const messages = text.split(/\r?\n\r?\n/);
    for (const msg of messages) {
        await sleep(150);
        const input = document.querySelector("div[class='ace-line']");
        if (!input) continue;
        input.innerText = msg;
        input.dispatchEvent(new Event("input", {
            bubbles: true
        }));

        await sleep(150);

        const sendBtn = document.querySelector(".e2e-send-msg-btn");
        if (sendBtn) {
            sendBtn.dispatchEvent(
                new MouseEvent("click", {
                    bubbles: true,
                    cancelable: true,
                    view: window
                })
            );
        }
    }
}

function switchToChat(username) {
    var chatlist = document.querySelector(".conversationConversationListwrapper").querySelectorAll("div[data-index]");
    var chatIndex = -1;
    for (let index = 0; index < chatlist.length; index++) {
        if(chatlist[index].innerText.split("\n")[0] == username){
            chatIndex = index;
        }
    }
    if (chatIndex === -1) return false;
    var element = chatlist[chatIndex].querySelector(".conversationConversationItemwrapper");

    element.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
    element.dispatchEvent(new MouseEvent("mouseup", { bubbles: true }));
    element.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    return true;
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
        }
        else if (data.type === "email") {
            console.log("收到邮件通知:", data.subject);
            await sendTextMsg(`📧 ${data.message}`);
        }
        else if (data.type === "proactive") {
            console.log("AI主动消息:", data.content);
            await sendTextMsg(data.content);
        }
        else if (data.type === "switchChat") {
            if (switchToChat(data.username)) {
                console.log("已切换到:", data.username);
            }
        }
        else if (data.type === "sendMessage") {
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

function isValidMessage(text) {
    if (!text) return false;
    const clean = text.trim();
    if (!clean) return false;
    const lines = clean
        .split("\n")
        .map(x => x.trim())
        .filter(x => x.length > 0);
    if (lines.length === 0) return false;
    if (lines.length === 1 && lines[0] === "分享评论") {
        return false;
    }
    if (lines[0] === "分享评论" && lines.length === 1) {
        return false;
    }
    return true;
}

async function startMessageListener() {
    console.log("聊天监听已启动");
    setInterval(async () => {
        const msg = document.querySelector(".messageMessageBoxcontentBox:not(.messageMessageBoxisFromMe)");
        const userName = document.querySelector(".RightPanelHeadertitle");
        if (!msg || !userName) return;
        if (userName.innerText.lastIndexOf("(") != -1) {
            const fromName = document.querySelector(".MessageBoxMessageTitleavatarName");
            if (!fromName || fromName.innerText !== username) return;
        }
        await sleep(250);
        const text = msg.innerText;
        if (!isValidMessage(text)) return;
        if (text !== lastMsg) {
            console.log("收到新消息:", text);
            lastMsg = text;
            const currentUser = document.querySelector(".RightPanelHeadertitle")?.innerText || "";
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    type: "message",
                    message: text,
                    userId: currentUser
                }));
            }
        }

    }, 1500);
}

function checkNewMessages() {
    const chatlist = document.querySelector(".conversationConversationListwrapper")?.querySelectorAll("div[data-index]");
    if (!chatlist) return;

    chatlist.forEach(chat => {
        const badges = chat.querySelectorAll(".semi-badge");
        if (badges.length === 0) return;

        const titleEl = chat.querySelector(".conversationConversationItemtitle");
        if (!titleEl) return;

        const chatUsername = titleEl.innerText.trim();

        if (!monitoredChats.has(chatUsername)) {
            monitoredChats.add(chatUsername);
            messageQueue.push(chatUsername);
            console.log("检测到新消息来自:", chatUsername, "队列长度:", messageQueue.length);

            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({
                    type: "newMessage",
                    username: chatUsername,
                    queueLength: messageQueue.length
                }));
            }
        }
    });

    chatlist.forEach(chat => {
        const badges = chat.querySelectorAll(".semi-badge");
        const titleEl = chat.querySelector(".conversationConversationItemtitle");
        if (!titleEl) return;

        const chatUsername = titleEl.innerText.trim();
        if (badges.length === 0 && monitoredChats.has(chatUsername)) {
            monitoredChats.delete(chatUsername);
        }
    });
}

function startNewMessageMonitor() {
    console.log("新消息监听已启动");

    const observer = new MutationObserver(() => checkNewMessages());

    const wrapper = document.querySelector(".conversationConversationListwrapper");
    if (wrapper) {
        observer.observe(wrapper, { childList: true, subtree: true });
        console.log("MutationObserver已启动");
    }

    setInterval(checkNewMessages, 3000);
}

window.switchToUser = function(username) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
            type: "aiSwitchChat",
            username: username
        }));
    }
};

window.getMessageQueue = function() {
    return messageQueue;
};

window.clearQueue = function() {
    messageQueue = [];
    monitoredChats.clear();
};

connectWebSocket();
startMessageListener();
startNewMessageMonitor();
