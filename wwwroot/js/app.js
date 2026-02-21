const elements = {
    status: document.getElementById('status'),
    messages: document.getElementById('messages'),
    name: document.getElementById('name'),
    message: document.getElementById('message'),
    sendBtn: document.getElementById('sendBtn'),
    clearBtn: document.getElementById('clearBtn')
};

let ws = null;
let isConnected = false;

function init() {
    connect();
    setupEventListeners();
    loadUserName();
}

function connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${protocol}//${location.host}/ws`;
    
    console.log('Connecting to', url);
    
    ws = new WebSocket(url);
    
    ws.onopen = onConnected;
    ws.onmessage = onMessage;
    ws.onclose = onDisconnected;
    ws.onerror = onError;
}

function onConnected() {
    isConnected = true;
    updateStatus('Connected', true);
    addMessage('You joined the chat!', 'system');
}

function onMessage(event) {
    try {
        const data = JSON.parse(event.data);
        
        const name = data.name ?? data.Name ?? 'Anonymous';
        const text = data.text ?? data.Text ?? '';
        const type = data.type ?? data.Type ?? 'message';
        
        if (type === 'clear') {
            clearMessages();
            return;
        }
        
        if (!text) return;
        
        const displayText = type === 'system' 
            ? text 
            : `${name}: ${text}`;
            
        const isOwn = name === getCurrentUserName() || name === elements.name.value.trim();
        
        addMessage(displayText, type, isOwn);
        
    } catch (err) {
        console.error('Parse error:', err);
    }
}

function onDisconnected(event) {
    isConnected = false;
    updateStatus('Disconnected', false);
    
    const reason = event.reason ? ': ' + event.reason : '';
    addMessage(`Connection closed${reason}`, 'system');
    
    setTimeout(() => {
        if (!isConnected) {
            console.log('Attempting reconnect...');
            connect();
        }
    }, 3000);
}

function onError(error) {
    console.error('WebSocket error:', error);
    addMessage('Connection error', 'system');
}

function addMessage(text, type, isOwn = false) {
    const div = document.createElement('div');
    div.className = `message ${type} ${isOwn ? 'own' : ''}`;
    div.textContent = text;
    elements.messages.appendChild(div);
    elements.messages.scrollTop = elements.messages.scrollHeight;
}

function sendMessage() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
    
    const name = elements.name.value.trim() || 'Anonymous';
    const text = elements.message.value.trim();
    
    if (!text) return;
    
    const payload = { name, text, type: 'message' };
    
    addMessage(`${name}: ${text}`, 'message', true);
    
    ws.send(JSON.stringify(payload));
    
    elements.message.value = '';
    elements.message.focus();
}

function updateStatus(text, connected) {
    elements.status.textContent = text;
    elements.status.className = `status ${connected ? 'connected' : 'disconnected'}`;
    
    elements.message.disabled = !connected;
    elements.sendBtn.disabled = !connected;
    elements.clearBtn.disabled = !connected;
}

function setupEventListeners() {
    elements.sendBtn.addEventListener('click', sendMessage);
    
    elements.message.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });
    
    elements.name.addEventListener('change', () => {
        saveUserName(elements.name.value.trim());
    });
    
    elements.clearBtn.addEventListener('click', clearChat);
}

function clearChat() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
    
    const name = elements.name.value.trim() || 'Anonymous';
    const payload = { name, text: 'clear', type: 'clear' };
    
    ws.send(JSON.stringify(payload));
}

function clearMessages() {
    elements.messages.innerHTML = '';
}

function getCurrentUserName() {
    return localStorage.getItem('chatUserName') || 'Anonymous';
}

function saveUserName(name) {
    if (name) {
        localStorage.setItem('chatUserName', name);
        elements.name.value = name;
    }
}

function loadUserName() {
    const name = getCurrentUserName();
    if (name && name !== 'Anonymous') {
        elements.name.value = name;
    }   
}

document.addEventListener('DOMContentLoaded', init);

