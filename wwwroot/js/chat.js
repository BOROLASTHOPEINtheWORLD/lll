window.ChatHelper = {
    connection: null,
    ticketId: null,
    currentUserId: null,
    pendingFiles: [],

    init: function (ticketId, currentUserId) {
        this.ticketId = ticketId;
        this.currentUserId = currentUserId;
        this.pendingFiles = [];

        // Создаем подключение к SignalR
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`/chathub`)
            .withAutomaticReconnect()
            .build();

        // Обработчики входящих сообщений
        this.connection.on('ReceiveMessage', (message) => {
            this.appendMessage(message);
            this.scrollToBottom();
        });

        this.connection.on('ReceiveMessageEdited', (data) => {
            this.updateMessage(data);
        });

        this.connection.on('ReceiveMessageDeleted', (commentId) => {
            this.deleteMessage(commentId);
        });

        this.connection.on('ReceiveError', (error) => {
            console.error('Chat error:', error);
            alert(error);
        });

        // Запуск подключения
        this.connection.start()
            .then(() => {
                console.log('SignalR Connected');
                return this.connection.invoke('JoinTicketRoom', ticketId);
            })
            .then(() => {
                console.log(`Joined ticket-${ticketId} room`);
            })
            .catch(err => console.error('SignalR Connection Error:', err));

        // Инициализация формы отправки
        this.initCommentForm();
    },

    initCommentForm: function () {
        const form = document.getElementById('comment-form');
        const fileInput = document.getElementById('comment-file-input');
        const attachBtn = document.getElementById('comment-attach-btn');
        const previewContainer = document.getElementById('comment-file-preview');

        if (attachBtn && fileInput) {
            attachBtn.addEventListener('click', () => fileInput.click());

            fileInput.addEventListener('change', (e) => {
                const files = Array.from(e.target.files);
                files.forEach(file => this.addPendingFile(file));
                fileInput.value = ''; // Сброс input
            });
        }

        if (form) {
            form.addEventListener('submit', (e) => this.handleSubmit(e));
        }
    },

    addPendingFile: function (file) {
        this.pendingFiles.push(file);
        this.renderPendingFiles();
    },

    removePendingFile: function (index) {
        this.pendingFiles.splice(index, 1);
        this.renderPendingFiles();
    },

    renderPendingFiles: function () {
        const container = document.getElementById('comment-file-preview');
        if (!container) return;

        container.innerHTML = '';
        this.pendingFiles.forEach((file, index) => {
            const preview = FilePreviewHelper.createFilePreview(
                file,
                () => this.removePendingFile(index),
                true
            );
            container.appendChild(preview);
        });
    },

    handleSubmit: async function (e) {
        e.preventDefault();

        const form = e.target;
        const contentInput = form.querySelector('#comment-content');
        const isInternalCheckbox = form.querySelector('#isInternal');

        const content = contentInput?.value.trim();
        const isInternal = isInternalCheckbox?.checked || false;

        if (!content && this.pendingFiles.length === 0) {
            alert('Введите текст или прикрепите файл');
            return;
        }

        try {
            let attachments = [];

            // Если есть файлы — загружаем их сначала
            if (this.pendingFiles.length > 0) {
                const uploadPromises = this.pendingFiles.map(file => {
                    const formData = new FormData();
                    formData.append('file', file);

                    return fetch('/Ticket/upload-attachment', {
                        method: 'POST',
                        body: formData,
                        headers: {
                            // ИСПРАВЛЕННАЯ СТРОКА НИЖЕ:
                            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                        }
                    })
                        .then(res => {
                            if (!res.ok) {
                                throw new Error(`Upload failed with status ${res.status}`);
                            }
                            return res.json();
                        })
                        .then(data => {
                            console.log('File uploaded:', data);
                            return data;
                        });
                });

                attachments = await Promise.all(uploadPromises);
                console.log('All attachments uploaded:', attachments);
            }

            // Теперь отправляем сообщение через SignalR с вложениями:
            console.log('Sending message with attachments:', attachments);
            await this.connection.invoke('SendMessage', this.ticketId, content, isInternal, attachments);
            console.log('Message sent successfully');

            // Очищаем форму
            if (contentInput) contentInput.value = '';
            this.pendingFiles = [];
            this.renderPendingFiles();
        } catch (err) {
            console.error('Error sending message:', err);
            alert(`Ошибка при отправке сообщения: ${err.message || err}`);
        }
    },

    appendMessage: function (message) {
        const container = document.getElementById('chat-messages');
        if (!container) return;

        // Удаляем "нет сообщений" если есть
        const noMessages = container.querySelector('.no-messages');
        if (noMessages) noMessages.remove();

        const isOwn = message.userId === this.currentUserId;
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${isOwn ? 'message-own' : ''} ${message.isInternal ? 'message-internal' : ''}`;
        messageDiv.dataset.commentId = message.id;

        const avatarHtml = message.authorAvatar
            ? `<img src="${message.authorAvatar}" alt="Avatar" />`
            : `<div class="avatar-placeholder">${(message.authorName?.[0] || '?').toUpperCase()}</div>`;

        const attachmentsHtml = message.attachments && message.attachments.length > 0
            ? `<div class="message-attachments">
        ${message.attachments.map(a => `
            <div class="AttachmentCell AttachmentCell--clickable"
                 onclick="window.open('${a.filePath}', '_blank')">
                <div class="AttachmentCell__imageBlockContainer">
                    <div class="AttachmentCell__imageBlockBackground"></div>
                    <div class="AttachmentCell__imageBlock">
                        <!-- Иконка документа -->
                        <svg aria-hidden="true" display="block" class="vkuiIcon vkuiIcon--24 vkuiIcon--w-24 vkuiIcon--h-24 vkuiIcon--document_list_outline_24"
                             width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M7.996 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8zM13 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8z"></path>
                            <path fill-rule="evenodd" d="M12.473 2c.3 0 .586 0 .866.066a2.4 2.4 0 0 1 .694.288c.245.15.447.353.659.565l4.389 4.39c.212.21.415.413.565.658a2.4 2.4 0 0 1 .288.694c.067.28.066.566.066.866v6.012c0 .947 0 1.713-.05 2.333-.053.64-.163 1.203-.43 1.726a4.4 4.4 0 0 1-1.922 1.922c-.523.267-1.087.377-1.726.43-.62.05-1.386.05-2.334.05h-3.076c-.948 0-1.714 0-2.334-.05-.64-.053-1.203-.163-1.726-.43a4.4 4.4 0 0 1-1.922-1.922c-.267-.523-.377-1.087-.43-1.726C4 17.252 4 16.486 4 15.538V8.462c0-.948 0-1.714.05-2.334.053-.64.163-1.203.43-1.726A4.4 4.4 0 0 1 6.402 2.48c.523-.267 1.087-.377 1.726-.43C8.748 2 9.514 2 10.462 2zM10.5 3.8H12v2.135c0 .53 0 .981.03 1.352.032.39.102.768.286 1.13a2.9 2.9 0 0 0 1.267 1.267c.362.184.741.254 1.13.286.37.03.822.03 1.351.03H18.2v5.5c0 .995 0 1.687-.045 2.226-.043.527-.123.828-.238 1.054a2.6 2.6 0 0 1-1.137 1.137c-.226.115-.527.195-1.055.238-.538.044-1.23.045-2.225.045h-3c-.995 0-1.687 0-2.226-.045-.527-.043-.828-.123-1.054-.238a2.6 2.6 0 0 1-1.137-1.137c-.115-.226-.195-.527-.238-1.055-.044-.538-.045-1.23-.045-2.225v-7c0-.995 0-1.687.045-2.225.043-.528.123-.829.238-1.055A2.6 2.6 0 0 1 7.22 4.083c.226-.115.527-.195 1.054-.238C8.813 3.8 9.505 3.8 10.5 3.8m3.3.773L17.427 8.2H16.1c-.575 0-.952 0-1.24-.024-.278-.023-.393-.062-.46-.096a1.1 1.1 0 0 1-.48-.48c-.034-.066-.073-.182-.096-.46A17 17 0 0 1 13.8 5.9z" clip-rule="evenodd"></path>
                        </svg>
                    </div>
                </div>
                <div class="AttachmentCell__infoBlockContainer">
                    <div class="AttachmentCell__infoBlock">
                        <h4 class="AttachmentCell__headline">${a.fileName}</h4>
                        <span class="AttachmentCell__footnote">${a.fileName.split('.').pop().toUpperCase()}</span>
                    </div>
                </div>
            </div>
        `).join('')}
       </div>`
            : '';

        const editedHtml = message.editedAt
            ? `<div class="message-edited">Отредактировано ${message.editedAt}</div>`
            : '';

        const actionsHtml = isOwn
            ? `<div class="message-actions">
                <button class="btn-edit-message" onclick="ChatHelper.editMessage(${message.id}, '${this.escapeHtml(message.content)}')">✏️</button>
                <button class="btn-delete-message" onclick="ChatHelper.deleteMessageRequest(${message.id})">🗑️</button>
               </div>`
            : '';

        messageDiv.innerHTML = `
            <div class="message-avatar">
                ${avatarHtml}
            </div>
            <div class="message-content">
                <div class="message-header">
                    <span class="message-author">${message.authorName}</span>
                    <span class="message-time">${message.createdAt}</span>
                    ${message.isInternal ? '<span class="badge-internal">Внутренний</span>' : ''}
                    ${actionsHtml}
                </div>
                <div class="message-text">${this.escapeHtml(message.content)}</div>
                ${attachmentsHtml}
                ${editedHtml}
            </div>
        `;

        container.appendChild(messageDiv);

        // Обновляем счетчик
        const counter = document.getElementById('comments-count');
        if (counter) {
            const count = parseInt(counter.textContent) || 0;
            counter.textContent = `${count + 1} сообщений`;
        }
    },

    updateMessage: function (data) {
        const messageDiv = document.querySelector(`[data-comment-id="${data.id}"]`);
        if (!messageDiv) return;

        const textDiv = messageDiv.querySelector('.message-text');
        if (textDiv) textDiv.textContent = data.content;

        let editedDiv = messageDiv.querySelector('.message-edited');
        if (!editedDiv) {
            editedDiv = document.createElement('div');
            editedDiv.className = 'message-edited';
            messageDiv.querySelector('.message-content').appendChild(editedDiv);
        }
        editedDiv.textContent = `Отредактировано ${data.editedAt}`;
    },

    deleteMessage: function (commentId) {
        const messageDiv = document.querySelector(`[data-comment-id="${commentId}"]`);
        if (messageDiv) {
            messageDiv.remove();
        }

        // Обновляем счетчик
        const container = document.getElementById('chat-messages');
        const counter = document.getElementById('comments-count');
        if (counter && container) {
            const count = container.querySelectorAll('.message').length;
            counter.textContent = `${count} сообщений`;

            if (count === 0) {
                container.innerHTML = `
                    <div class="no-messages">
                        <p>Сообщений пока нет</p>
                        <span>Будьте первым, кто напишет комментарий</span>
                    </div>
                `;
            }
        }
    },

    deleteMessageRequest: async function (commentId) {
        if (!confirm('Вы уверены, что хотите удалить сообщение?')) return;

        try {
            await this.connection.invoke('DeleteMessage', commentId);
        } catch (err) {
            console.error('Error deleting message:', err);
            alert('Ошибка при удалении сообщения');
        }
    },

    editMessage: function (commentId, currentContent) {
        const newContent = prompt('Редактировать сообщение:', currentContent);
        if (newContent && newContent !== currentContent) {
            this.connection.invoke('EditMessage', commentId, newContent)
                .catch(err => {
                    console.error('Error editing message:', err);
                    alert('Ошибка при редактировании сообщения');
                });
        }
    },

    scrollToBottom: function () {
        const container = document.getElementById('chat-messages');
        if (container) {
            container.scrollTop = container.scrollHeight;
        }
    },

    escapeHtml: function (text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    disconnect: function () {
        if (this.connection) {
            this.connection.invoke('LeaveTicketRoom', this.ticketId)
                .then(() => this.connection.stop())
                .catch(err => console.error('Error disconnecting:', err));
        }
    }
};