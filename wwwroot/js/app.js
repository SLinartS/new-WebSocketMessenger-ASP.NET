const elements = {
    status: document.getElementById('status'),
    messages: document.getElementById('messages'),
    name: document.getElementById('name'),
    message: document.getElementById('message'),
    sendBtn: document.getElementById('sendBtn'),
    clearBtn: document.getElementById('clearBtn'),
    usersList: document.getElementById('usersList'),
    typingIndicator: document.getElementById('typingIndicator'),
    chatList: document.getElementById('chatList'),
    findUserInput: document.getElementById('findUserInput'),
    findUserBtn: document.getElementById('findUserBtn'),
    currentChatInfo: document.getElementById('currentChatInfo'),
    myUserId: document.getElementById('myUserId'),
    copyUserIdBtn: document.getElementById('copyUserIdBtn')
};

let ws = null;
let isConnected = false;
let userId = null;
let typingTimeout = null;
let typingUsers = new Map();
let currentChatId = 'general';
let chatList = [];
let chatParticipants = new Map(); // chatId -> Set of userIds

function init() {
    userId = getOrCreateUserId();
    connect();
    setupEventListeners();
    loadUserName();
    loadCurrentChat();
    displayMyUserId();
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

    // Load chat list
    loadChatList();
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

        // Handle chat update message (chat switch)
        if (type === 'chatUpdate') {
            currentChatId = data.chatRoomId;
            saveCurrentChat(currentChatId);
            renderMessages(data.messages);
            renderOnlineUsers(data.users);
            updateCurrentChatInfo();

            // Update chat participants for filtering
            if (data.users) {
                const participants = new Set(data.users.map(u => u.id));
                chatParticipants.set(currentChatId, participants);
            }
            return;
        }

        // Handle users list message
        if (type === 'usersList') {
            refreshUserList(data, currentChatId)
            return;
        }

        // Handle typing status message
        if (type === 'typing') {
            updateTypingIndicator(data.userId, data.nickname, data.isTyping);
            return;
        }

        if (type === 'clear') {
            if (data.chatRoomId === currentChatId || !data.chatRoomId) {
                clearMessages();
            }
            return;
        }

        if (!text) return;

        const timestamp = data.timestamp ?? data.Timestamp ?? null;

        // Only show message if it's in current chat
        if (data.chatRoomId === currentChatId || !data.chatRoomId) {
            const isOwn = name === getCurrentUserName() || name === elements.name.value.trim();

            addMessage(text, type, isOwn, name, timestamp);
        }

    } catch (err) {
        console.error('Parse error:', err);
    }
}

function refreshUserList(data, currentChatId) {
    renderUsersList(data.users);
    updateTypingUsersFromList(data.users);

    // Update chat participants for filtering
    if (data.users && currentChatId) {
        const participants = new Set(data.users.map(u => u.id));
        chatParticipants.set(currentChatId, participants);
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

function addMessage(text, type, isOwn = false, name = null, timestamp = null) {
    const div = document.createElement('div');
    div.className = `message ${type} ${isOwn ? 'own' : ''}`;

    if (type === 'system') {
        // System messages: just text
        div.textContent = text;
    } else {
        // Regular messages: name, text, time
        const nameText = name ?? 'Anonymous';
        const timeText = timestamp ? formatTime(timestamp) : '';

        div.innerHTML = `
            <div class="message-header">
                <span class="message-name">${escapeHtml(nameText)}</span>
                ${timeText ? `<span class="message-time">${timeText}</span>` : ''}
            </div>
            <div class="message-text">${escapeHtml(text)}</div>
        `;
    }

    elements.messages.appendChild(div);
    elements.messages.scrollTop = elements.messages.scrollHeight;
}

function formatTime(timestamp) {
    const date = new Date(timestamp);
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${hours}:${minutes}`;
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function renderMessages(messages) {
    elements.messages.innerHTML = '';
    if (!messages || messages.length === 0) return;

    messages.forEach(msg => {
        const name = msg.name ?? 'Anonymous';
        const text = msg.text ?? '';
        const type = msg.type ?? 'message';
        const timestamp = msg.timestamp ?? null;

        if (!text) return;

        const isOwn = name === getCurrentUserName() || name === elements.name.value.trim();

        addMessage(text, type, isOwn, name, timestamp);
    });
}

function sendMessage() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;

    const name = elements.name.value.trim() || 'Anonymous';
    const text = elements.message.value.trim();

    if (!text) return;

    const payload = { name, text, type: 'message', chatRoomId: currentChatId };

    addMessage(text, 'message', true, name);

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

    // Chat list event listeners
    elements.findUserBtn.addEventListener('click', findUserById);
    elements.findUserInput.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') findUserById();
    });

    if (elements.copyUserIdBtn) {
        elements.copyUserIdBtn.addEventListener('click', copyUserIdToClipboard);
    }
}

function clearChat() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;

    const name = elements.name.value.trim() || 'Anonymous';
    const payload = { name, text: 'clear', type: 'clear', chatRoomId: currentChatId };

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

function loadCurrentChat() {
    const savedChat = localStorage.getItem('currentChatId');
    if (savedChat) {
        currentChatId = savedChat;
    }
}

function saveCurrentChat(chatId) {
    localStorage.setItem('currentChatId', chatId);
}

function renderUsersList(users) {
    if (!elements.usersList) return;

    if (!users || users.length === 0) {
        elements.usersList.innerHTML = '<div class="users-count">No active users</div>';
        return;
    }

    // Filter users to show only those in current chat
    const participants = chatParticipants.get(currentChatId) || new Set();
    const filteredUsers = users.filter(user =>
        currentChatId === 'general' || participants.has(user.id)
    );

    const html = filteredUsers.map(user => `
        <div class="user-item">
            <div class="user-nickname">${escapeHtml(user.nickname || 'Anonymous')}</div>
            <div class="user-details">
                <span class="user-id">ID: ${escapeHtml(user.id || '')}</span>
            </div>
        </div>
    `).join('');

    elements.usersList.innerHTML = html + `<div class="users-count">${filteredUsers.length} user(s) online</div>`;
}

function renderOnlineUsers(users) {
    if (!elements.usersList) return;

    if (!users || users.length === 0) {
        elements.usersList.innerHTML = '<div class="users-count">No active users</div>';
        return;
    }

    const html = users.map(user => `
        <div class="user-item">
            <div class="user-nickname">${escapeHtml(user.nickname || 'Anonymous')}</div>
            <div class="user-details">
                <span class="user-id">ID: ${escapeHtml(user.id || '')}</span>
            </div>
        </div>
    `).join('');

    elements.usersList.innerHTML = html + `<div class="users-count">${users.length} user(s) online</div>`;
}

function sendTypingStatus() {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;

    const name = elements.name.value.trim() || 'Anonymous';
    const payload = { name, type: 'typing', isTyping: true, chatRoomId: currentChatId };

    ws.send(JSON.stringify(payload));

    // Clear existing timeout
    if (typingTimeout) {
        clearTimeout(typingTimeout);
    }

    // Set timeout to stop typing indicator after 2 seconds
    typingTimeout = setTimeout(() => {
        const stopPayload = { name, type: 'typing', isTyping: false, chatRoomId: currentChatId };
        ws.send(JSON.stringify(stopPayload));
    }, 2000);
}

function updateTypingIndicator(typingUserId, nickname, isTyping) {
    // Don't show own typing status
    if (typingUserId === userId) return;

    // Only show if in current chat
    const participants = chatParticipants.get(currentChatId) || new Set();
    if (currentChatId !== 'general' && !participants.has(typingUserId)) return;

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
            // Only add if in current chat
            const participants = chatParticipants.get(currentChatId) || new Set();
            if (currentChatId === 'general' || participants.has(user.id)) {
                typingUsers.set(user.id, user.nickname);
            }
        }
    });

    renderTypingIndicator();
}

// Chat list functions
async function loadChatList() {
    try {
        const response = await fetch('/api/chats');
        if (response.ok) {
            chatList = await response.json();

            // Ensure general chat exists in the list
            const generalChat = chatList.find(c => c.id === 'general');
            if (!generalChat) {
                // Add general chat at the beginning
                chatList.unshift({
                    id: 'general',
                    name: 'Общий чат',
                    createdAt: new Date().toISOString(),
                    isPrivate: false
                });
            }

            renderChatList();
        }
    } catch (err) {
        console.error('Error loading chat list:', err);
    }
}

function renderChatList() {
    if (!elements.chatList) return;

    if (!chatList || chatList.length === 0) {
        elements.chatList.innerHTML = '<div class="chat-item">No chats available</div>';
        return;
    }

    const html = chatList.map(chat => `
        <div class="chat-item ${chat.id === currentChatId ? 'active' : ''}" data-chat-id="${chat.id}">
            <div class="chat-info">
                <div class="chat-name">${escapeHtml(chat.name)}</div>
                <div class="chat-meta">${chat.isPrivate ? 'Private' : 'Public'} chat</div>
            </div>
            ${chat.id !== 'general' ? `<button class="chat-delete-btn" data-chat-id="${chat.id}">×</button>` : ''}
        </div>
    `).join('');

    elements.chatList.innerHTML = html;

    // Add event listeners to chat items
    document.querySelectorAll('.chat-item').forEach(item => {
        item.addEventListener('click', () => {
            const chatId = item.dataset.chatId;
            if (chatId) {
                switchChat(chatId);
            }
        });
    });

    document.querySelectorAll('.chat-delete-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const chatId = btn.dataset.chatId;
            if (chatId) {
                deleteChat(chatId);
            }
        });
    });
}

async function switchChat(chatId) {
    if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;

    currentChatId = chatId;
    saveCurrentChat(chatId);

    // Clear messages and users list
    clearMessages();
    elements.usersList.innerHTML = '<div class="users-count">Loading...</div>';

    // Update chat list UI
    renderChatList();

    // Update current chat info
    updateCurrentChatInfo();
    await refreshMessages(chatId);

    // Send switch chat command via WebSocket
    const payload = { type: 'switchChat', chatRoomId: chatId };
    ws.send(JSON.stringify(payload));
}


async function refreshMessages(chatId) {
    // Fetch messages from API as backup
    try {
        const response = await fetch(`/api/chats/${chatId}/messages`);
        if (response.ok) {
            const messages = await response.json();
            renderMessages(messages);
        }
    } catch (err) {
        console.error('Error fetching messages:', err);
    }
}

function updateCurrentChatInfo() {
    if (!elements.currentChatInfo) return;

    const chat = chatList.find(c => c.id === currentChatId);
    if (chat) {
        elements.currentChatInfo.textContent = `Current chat: ${chat.name}`;
    } else {
        elements.currentChatInfo.textContent = 'Current chat: Unknown';
    }
}

async function findUserById() {
    const targetUserId = elements.findUserInput.value.trim();

    if (!targetUserId) {
        alert('Please enter a user ID');
        return;
    }

    try {
        const response = await fetch('/api/chats/find-user', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId, targetUserId })
        });

        if (response.ok) {
            const result = await response.json();

            if (result.found) {
                // Create private chat with found user
                await createPrivateChat(targetUserId);
                elements.findUserInput.value = '';
            } else {
                alert(result.message || 'User not found');
            }
        }
    } catch (err) {
        console.error('Error finding user:', err);
        alert('Error finding user');
    }
}

async function createPrivateChat(targetUserId) {
    try {
        const response = await fetch('/api/chats', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId, targetUserId })
        });

        if (response.ok) {
            const chat = await response.json();

            // Update chat list
            await loadChatList();

            // Switch to new chat
            switchChat(chat.id);
        }
    } catch (err) {
        console.error('Error creating private chat:', err);
        alert('Error creating private chat');
    }
}

async function deleteChat(chatId) {
    if (chatId === 'general') {
        alert('Cannot delete general chat');
        return;
    }

    if (!confirm('Are you sure you want to delete this chat? All messages will be permanently removed.')) {
        return;
    }

    try {
        const response = await fetch(`/api/chats/${chatId}?userId=${encodeURIComponent(userId)}`, {
            method: 'DELETE'
        });

        if (response.ok) {
            // Remove from local list
            chatList = chatList.filter(c => c.id !== chatId);
            renderChatList();

            // If current chat was deleted, switch to general
            if (currentChatId === chatId) {
                switchChat('general');
            }
        }
    } catch (err) {
        console.error('Error deleting chat:', err);
        alert('Error deleting chat');
    }
}

// Display user's ID
function displayMyUserId() {
    if (elements.myUserId && userId) {
        elements.myUserId.textContent = userId;
    }
}

// Copy user ID to clipboard
function copyUserIdToClipboard() {
    if (userId) {
        navigator.clipboard.writeText(userId).then(() => {
            const originalText = elements.copyUserIdBtn.textContent;
            elements.copyUserIdBtn.textContent = 'Copied!';
            setTimeout(() => {
                elements.copyUserIdBtn.textContent = originalText;
            }, 2000);
        }).catch(err => {
            console.error('Failed to copy:', err);
            alert('Failed to copy user ID');
        });
    }
}

// Initialize on DOM ready
document.addEventListener('DOMContentLoaded', init);
