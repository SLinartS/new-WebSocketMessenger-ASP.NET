const elements = {
    status: document.getElementById('status'),
    messages: document.getElementById('messages'),
    name: document.getElementById('name'),
    message: document.getElementById('message'),
    sendBtn: document.getElementById('sendBtn'),
    clearBtn: document.getElementById('clearBtn'),
    usersList: document.getElementById('usersList'),
    typingIndicator: document.getElementById('typingIndicator')
};

let ws = null;
let isConnected = false;
let userId = null;
let typingTimeout = null;
let typingUsers = new Map();

function init() {
    userId = getOrCreateUserId();
    connect();
    setupEventListeners();
    loadUserName();
}

function getOrCreateUserId() {
    let id = localStorage.getItem('chatUserId');
    if (!id) {
        id = generateUuid();
        localStorage.setItem('chatUserId', id);
    }
    return id;
}

function generateUuid() {
    // Generate short 8-character ID
    return Math.random().toString(36).substring(2, 10);
}

function connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${protocol}//${location.host}/ws?userId=${userId}`;
    
    console.log('Connecting to', url, 'with userId', userId);
    
    ws = new WebSocket(url);
    
    ws.onopen = onConnected;
    ws.onmessage = onMessage;
    ws.onclose = onDisconnected;
    ws.onerror = onError;
}

function onConnected() {
    isConnected = true;
    updateStatus('Connected', true);
    
    // Send nickname to server immediately after connection
    sendNickname();
}

function sendNickname() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
    
    const name = elements.name.value.trim() || 'Anonymous';
    const payload = { name, text: '', type: 'nickname' };
    
    ws.send(JSON.stringify(payload));
}

function onMessage(event) {
    try {
        const data = JSON.parse(event.data);
        
        const name = data.name ?? data.Name ?? 'Anonymous';
        const text = data.text ?? data.Text ?? '';
        const type = data.type ?? data.Type ?? 'message';
        
        // Handle users list message
        if (type === 'usersList') {
            renderUsersList(data.users);
            updateTypingUsersFromList(data.users);
            return;
        }
        
        // Handle typing status message
        if (type === 'typing') {
            updateTypingIndicator(data.userId, data.nickname, data.isTyping);
            return;
        }
        
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
    
    // Clear users list on disconnect
    if (elements.usersList) {
        elements.usersList.innerHTML = '<div class="users-count">No active users</div>';
    }
    
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
    
    elements.message.addEventListener('input', () => {
        sendTypingStatus();
    });
    
    elements.name.addEventListener('change', () => {
        saveUserName(elements.name.value.trim());
        sendNickname();
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

function renderUsersList(users) {
    if (!elements.usersList) return;
    
    if (!users || users.length === 0) {
        elements.usersList.innerHTML = '<div class="users-count">No active users</div>';
        return;
    }
    
    const html = users.map(user => `
        <div class="user-item">
            <div class="user-nickname">${escapeHtml(user.nickname || 'Anonymous')}</div>
            <div class="user-details">
                <span class="user-ip">${escapeHtml(user.ipAddress || 'unknown')}</span>
                <span class="user-id">ID: ${escapeHtml(user.id || '')}</span>
            </div>
        </div>
    `).join('');
    
    elements.usersList.innerHTML = html + `<div class="users-count">${users.length} user(s) online</div>`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function sendTypingStatus() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
    
    const name = elements.name.value.trim() || 'Anonymous';
    const payload = { name, type: 'typing', isTyping: true };
    
    ws.send(JSON.stringify(payload));
    
    // Clear existing timeout
    if (typingTimeout) {
        clearTimeout(typingTimeout);
    }
    
    // Set timeout to stop typing indicator after 2 seconds
    typingTimeout = setTimeout(() => {
        const stopPayload = { name, type: 'typing', isTyping: false };
        ws.send(JSON.stringify(stopPayload));
    }, 2000);
}

function updateTypingIndicator(typingUserId, nickname, isTyping) {
    // Don't show own typing status
    if (typingUserId === userId) return;
    
    if (isTyping) {
        typingUsers.set(typingUserId, nickname);
    } else {
        typingUsers.delete(typingUserId);
    }
    
    renderTypingIndicator();
}

function renderTypingIndicator() {
    if (!elements.typingIndicator) return;
    
    const typingUsersArray = Array.from(typingUsers.values());
    
    if (typingUsersArray.length === 0) {
        elements.typingIndicator.textContent = '';
        return;
    }
    
    let text;
    if (typingUsersArray.length === 1) {
        text = `${typingUsersArray[0]} is typing...`;
    } else if (typingUsersArray.length === 2) {
        text = `${typingUsersArray[0]} and ${typingUsersArray[1]} are typing...`;
    } else {
        text = `${typingUsersArray.length} users typing...`;
    }
    
    elements.typingIndicator.textContent = text;
}

function updateTypingUsersFromList(users) {
    // Clear current typing users
    typingUsers.clear();
    
    // Add users who are currently typing (excluding self)
    users.forEach(user => {
        if (user.id !== userId && user.isTyping) {
            typingUsers.set(user.id, user.nickname);
        }
    });
    
    renderTypingIndicator();
}

document.addEventListener('DOMContentLoaded', init);
