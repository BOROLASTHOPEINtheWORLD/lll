window.ChatHelper = {
    connection: null,
    ticketId: null,
    currentUserId: null,
    pendingFiles: [],
    removedAttachments: [],
    editingCommentId: null,
    originalContent: '',

    init: function (ticketId, currentUserId) {
        this.ticketId = ticketId;
        this.currentUserId = currentUserId;
        this.pendingFiles = [];

        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`/chathub`)
            .withAutomaticReconnect()
            .build();

        this.connection.on('ReceiveMessage', (message) => {
            this.appendMessage(message);
            this.scrollToBottom();

            const countEl = document.querySelector('.message-count');
            if (countEl) {
                const count = parseInt(countEl.textContent) || 0;
                countEl.textContent = `${count + 1} комментариев`;
            }
        });

        this.connection.on('ReceiveMessageEdited', (data) => {
            this.updateMessage(data);
        });

        this.connection.on('ReceiveMessageDeleted', (commentId) => {
            this.deleteMessage(commentId);

            const countEl = document.querySelector('.message-count');
            if (countEl) {
                const count = parseInt(countEl.textContent) || 1;
                countEl.textContent = `${count - 1} комментариев`;
            }
        });

        this.connection.on('ReceiveError', (error) => {
            console.error('Chat error:', error);
            alert(error);
        });

        this.connection.start()
            .then(() => {
                console.log('SignalR Connected');
                return this.connection.invoke('JoinTicketRoom', ticketId);
            })
            .then(() => {
                console.log(`Joined ticket-${ticketId} room`);
            })
            .catch(err => console.error('SignalR Connection Error:', err));

        this.initCommentForm();
        this.processExistingAttachments();
        this.initContextMenu();
        this.processExistingMessages();
        setTimeout(() => {
            this.processExistingMessages();
        }, 100);

    },



    initContextMenu: function () {
        const ctxMenu = document.getElementById('context-menu');
        const ctxEditBtn = document.getElementById('ctx-edit-btn');
        const ctxDeleteBtn = document.getElementById('ctx-delete-btn');

        if (!ctxMenu) return;

        document.addEventListener('click', () => {
            ctxMenu.style.display = 'none';
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                ctxMenu.style.display = 'none';
            }
        });

        document.addEventListener('contextmenu', (e) => {
            const messageEl = e.target.closest('.message');
            if (messageEl) {
                e.preventDefault();

                const isOwn = messageEl.classList.contains('message-own');
                if (!isOwn) return;

                const commentId = parseInt(messageEl.dataset.commentId);
                if (isNaN(commentId)) return;

                const contentEl = messageEl.querySelector('.message-text');
                const currentContent = contentEl?.textContent || '';

                ctxMenu.style.display = 'block';
                ctxMenu.style.left = e.pageX + 'px';
                ctxMenu.style.top = e.pageY + 'px';

                if (ctxEditBtn) {
                    ctxEditBtn.onclick = () => {
                        this.editMessage(commentId, currentContent);
                        ctxMenu.style.display = 'none';
                    };
                }

                if (ctxDeleteBtn) {
                    ctxDeleteBtn.onclick = () => {
                        this.deleteMessageRequest(commentId);
                        ctxMenu.style.display = 'none';
                    };
                }
            }
        });

        document.addEventListener('click', (e) => {
            const optionsBtn = e.target.closest('.message-options-btn');
            if (optionsBtn) {
                e.stopPropagation();
                const messageEl = optionsBtn.closest('.message');
                if (!messageEl) return;

                const commentId = parseInt(messageEl.dataset.commentId);
                if (isNaN(commentId)) return;

                const contentEl = messageEl.querySelector('.message-text');
                const currentContent = contentEl?.textContent || '';

                const rect = optionsBtn.getBoundingClientRect();
                ctxMenu.style.display = 'block';
                ctxMenu.style.left = (rect.left - 180) + 'px';
                ctxMenu.style.top = rect.bottom + 'px';

                if (ctxEditBtn) {
                    ctxEditBtn.onclick = () => {
                        this.editMessage(commentId, currentContent);
                        ctxMenu.style.display = 'none';
                    };
                }

                if (ctxDeleteBtn) {
                    ctxDeleteBtn.onclick = () => {
                        this.deleteMessageRequest(commentId);
                        ctxMenu.style.display = 'none';
                    };
                }
            }
        });
    },

    processExistingMessages: function () {
        console.log('processExistingMessages called');
        const messageElements = document.querySelectorAll('.message');
        console.log('Found messages:', messageElements.length);

        messageElements.forEach((messageEl, index) => {
            const timeElement = messageEl.querySelector('.message-time');
            console.log(`Message ${index}:`, {
                timeElement: !!timeElement,
                dataTime: timeElement?.getAttribute('data-time'),
                currentText: timeElement?.textContent
            });

            if (!timeElement) return;

            const isoTime = timeElement.getAttribute('data-time');
            if (!isoTime) return;

            const formattedTime = this.formatTime(isoTime);
            console.log(`Message ${index} formatted:`, formattedTime);
            timeElement.textContent = formattedTime;
        });
    },

    processExistingAttachments: function () {
        const existingAttachments = document.querySelectorAll('.message-attachments .AttachmentCell');
        existingAttachments.forEach(cell => {
            const fileName = cell.querySelector('.AttachmentCell__headline')?.textContent;
            if (!fileName) return;

            const ext = fileName.split('.').pop().toLowerCase();
            const isImage = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext);

            if (isImage) {
                const iconBlock = cell.querySelector('.AttachmentCell__imageBlock');
                if (iconBlock) {
                    iconBlock.innerHTML = '';

                    const onClickAttr = cell.getAttribute('onclick');
                    if (onClickAttr && onClickAttr.includes("window.open")) {
                        const filePath = onClickAttr.match(/'([^']+)'/)[1];

                        const img = document.createElement('img');
                        img.src = filePath;
                        img.style.width = '100%';
                        img.style.height = '100%';
                        img.style.objectFit = 'cover';
                        img.style.borderRadius = '4px';
                        img.loading = 'lazy';

                        iconBlock.appendChild(img);
                        iconBlock.classList.add('image-preview');

                        cell.removeAttribute('onclick');
                        cell.classList.add('AttachmentCell--clickable');
                        cell.addEventListener('click', () => {
                            window.FilePreviewHelper.openImageModal(filePath, fileName);
                        });
                    }
                }
            }
        });
    },

    initCommentForm: function () {
        const form = document.getElementById('comment-form');
        const fileInput = document.getElementById('comment-file-input');
        const contentInput = document.getElementById('comment-content');

        if (contentInput) {
            contentInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    const content = contentInput.value.trim();
                    const hasFiles = this.pendingFiles.length > 0;
                    if (content || hasFiles) {
                        form.dispatchEvent(new Event('submit'));
                    }
                }
            });
        }

        if (fileInput) {
            if (fileInput._changeHandler) {
                fileInput.removeEventListener('change', fileInput._changeHandler);
            }

            const changeHandler = (e) => {
                const files = Array.from(e.target.files);
                files.forEach(file => {
                    this.addPendingFile(file);
                });
                fileInput.value = '';
            };

            fileInput._changeHandler = changeHandler;
            fileInput.addEventListener('change', changeHandler);
        }

        if (form) {
            form.removeEventListener('submit', this._submitHandler);
            this._submitHandler = (e) => this.handleSubmit(e);
            form.addEventListener('submit', this._submitHandler);
        }
    },

    addPendingFile: function (file) {
        if (!file.id) {
            file.id = Date.now() + Math.random();
        }

        this.pendingFiles.push(file);
        this.renderPendingFiles();
    },

    removePendingFile: function (file) {
        this.pendingFiles = this.pendingFiles.filter(f => f.id !== file.id);
        this.renderPendingFiles();
    },

    renderPendingFiles: function () {
        const container = document.getElementById('comment-file-preview');
        if (!container) {
            console.error('Container #comment-file-preview not found');
            return;
        }

        container.innerHTML = '';

        this.pendingFiles.forEach((file) => {
            let preview;

            if (file.path || file.filePath) {
                preview = FilePreviewHelper.createVirtualFilePreview(
                    {
                        id: file.id,
                        fileName: file.fileName,
                        filePath: file.path || file.filePath
                    },
                    () => this.removeVirtualFile(file),
                    true
                );
            } else {
                preview = FilePreviewHelper.createFilePreview(
                    file,
                    () => this.removePendingFile(file),
                    true
                );
            }

            container.appendChild(preview);
        });
    },

    resetCommentForm: function () {
        const contentInput = document.getElementById('comment-content');
        if (contentInput) contentInput.value = '';

        this.editingCommentId = null;
        this.originalContent = '';
        this.pendingFiles = [];
        this.removedAttachments = [];
        this.renderPendingFiles();
    },

    handleSubmit: async function (e) {
        e.preventDefault();

        const form = e.target;
        const contentInput = form.querySelector('#comment-content');
        const isInternalCheckbox = form.querySelector('#isInternal');

        const content = contentInput?.value.trim();

        if (!content && this.pendingFiles.length === 0) {
            this.showError('Введите текст сообщения или прикрепите файлы');
            return;
        }

        try {
            const tokenElement = document.querySelector('input[name="__RequestVerificationToken"]');
            const token = tokenElement ? tokenElement.value : '';

            const formData = new FormData();
            formData.append('ticketId', this.ticketId);
            formData.append('content', content);
            formData.append('isInternal', isInternalCheckbox?.checked || false);

            if (token) {
                formData.append('__RequestVerificationToken', token);
            }

            this.pendingFiles.forEach(file => {
                if (file.path || file.filePath) {
                    formData.append('existingAttachments', file.path || file.filePath);
                } else {
                    formData.append('attachments', file);
                }
            });

            this.removedAttachments.forEach(path => {
                formData.append('removedAttachments', path);
            });

            let response;

            if (this.editingCommentId) {
                formData.append('commentId', this.editingCommentId);
                response = await fetch('/Ticket/EditComment', {
                    method: 'POST',
                    body: formData
                });
            } else {
                response = await fetch('/Ticket/AddComment', {
                    method: 'POST',
                    body: formData
                });
            }

            if (!response.ok) {
                const text = await response.text();
                throw new Error(`Ошибка сервера: ${response.status}. ${text}`);
            }

            if (contentInput) contentInput.value = '';
            this.resetCommentForm();

            console.log('Comment sent via AJAX, waiting for SignalR broadcast...');
        } catch (err) {
            console.error('Error sending message:', err);
            this.showError(`Ошибка при отправке: ${err.message || err}`);
        }
    },

    showError: function (message) {
        let errorEl = document.querySelector('.comment-error-message');
        if (!errorEl) {
            errorEl = document.createElement('div');
            errorEl.className = 'comment-error-message';
            errorEl.style.cssText = `
                background: #fee;
                color: #c33;
                padding: 8px 12px;
                border-radius: 4px;
                margin-top: 8px;
                font-size: 14px;
                border-left: 3px solid #f55;
            `;
            const form = document.getElementById('comment-form');
            if (form) form.parentNode.insertBefore(errorEl, form);
        }

        errorEl.textContent = message;
        errorEl.style.display = 'block';

        setTimeout(() => {
            if (errorEl) errorEl.style.display = 'none';
        }, 5000);
    },

    generateAttachmentsHtml: function (attachments) {
        if (!attachments || attachments.length === 0) return '';

        const container = document.createElement('div');
        container.className = 'message-attachments';

        attachments.forEach(attachment => {
            const ext = attachment.fileName.split('.').pop().toLowerCase();
            const isImage = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext);

            const attachmentDiv = document.createElement('div');
            attachmentDiv.className = 'AttachmentCell AttachmentCell--clickable';

            const imageBlockContainer = document.createElement('div');
            imageBlockContainer.className = 'AttachmentCell__imageBlockContainer';

            const bg = document.createElement('div');
            bg.className = 'AttachmentCell__imageBlockBackground';
            imageBlockContainer.appendChild(bg);

            const imageBlock = document.createElement('div');
            imageBlock.className = 'AttachmentCell__imageBlock';

            if (isImage) {
                const img = document.createElement('img');
                img.src = attachment.filePath;
                img.style.width = '100%';
                img.style.height = '100%';
                img.style.objectFit = 'cover';
                img.style.borderRadius = '4px';
                img.loading = 'lazy';
                imageBlock.appendChild(img);
                imageBlock.classList.add('image-preview');
            } else {
                imageBlock.innerHTML = `
                    <svg aria-hidden="true" display="block" width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M7.996 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8zM13 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8z"></path>
                        <path fill-rule="evenodd" d="M12.473 2c.3 0 .586 0 .866.066a2.4 2.4 0 0 1 .694.288c.245.15.447.353.659.565l4.389 4.39c.212.21.415.413.565.658a2.4 2.4 0 0 1 .288.694c.067.28.066.566.066.866v6.012c0 .947 0 1.713-.05 2.333-.053.64-.163 1.203-.43 1.726a4.4 4.4 0 0 1-1.922 1.922c-.523.267-1.087.377-1.726.43-.62.05-1.386.05-2.334.05h-3.076c-.948 0-1.714 0-2.334-.05-.64-.053-1.203-.163-1.726-.43a4.4 4.4 0 0 1-1.922-1.922c-.267-.523-.377-1.087-.43-1.726C4 17.252 4 16.486 4 15.538V8.462c0-.948 0-1.714.05-2.334.053-.64.163-1.203.43-1.726A4.4 4.4 0 0 1 6.402 2.48c.523-.267 1.087-.377 1.726-.43C8.748 2 9.514 2 10.462 2z"></path>
                    </svg>`;
            }

            imageBlockContainer.appendChild(imageBlock);

            const infoBlockContainer = document.createElement('div');
            infoBlockContainer.className = 'AttachmentCell__infoBlockContainer';

            const infoBlock = document.createElement('div');
            infoBlock.className = 'AttachmentCell__infoBlock';

            const headline = document.createElement('h4');
            headline.className = 'AttachmentCell__headline';
            const displayName = attachment.fileName.length > 25 ? attachment.fileName.substring(0, 22) + '...' : attachment.fileName;
            headline.textContent = displayName;
            headline.title = attachment.fileName;

            const footnote = document.createElement('span');
            footnote.className = 'AttachmentCell__footnote';
            footnote.textContent = `${ext.toUpperCase()} • Загружен`;

            infoBlock.appendChild(headline);
            infoBlock.appendChild(footnote);
            infoBlockContainer.appendChild(infoBlock);

            attachmentDiv.appendChild(imageBlockContainer);
            attachmentDiv.appendChild(infoBlockContainer);

            if (isImage) {
                attachmentDiv.addEventListener('click', () => {
                    window.FilePreviewHelper.openImageModal(attachment.filePath, attachment.fileName);
                });
            } else {
                attachmentDiv.addEventListener('click', () => {
                    window.open(attachment.filePath, '_blank');
                });
            }

            container.appendChild(attachmentDiv);
        });

        return container.innerHTML;
    },

    appendMessage: function (message) {
        const container = document.getElementById('chat-messages');
        if (!container) return;

        const noMessages = container.querySelector('.no-messages');
        if (noMessages) noMessages.remove();

        // Если это системное сообщение (userId === null) — отрисовываем его по-особому и выходим
        if (message.userId === null) {
            const messageDiv = document.createElement('div');
            messageDiv.className = 'message message-system';
            messageDiv.dataset.commentId = message.id;

            const timeFormatted = this.formatTime(message.createdAt);
            messageDiv.innerHTML = `
        <div class="system-message-content">
            <svg class="system-icon" width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 15h2v2h-2v-2zm0-10h2v8h-2V7z"/>
            </svg>
            <span class="system-message-text">${this.escapeHtml(message.content).replace(/\n/g, '<br>')}</span>
            <span class="message-time system-time" data-time="${message.createdAt}">${timeFormatted}</span>
        </div>
    `;
            container.appendChild(messageDiv);
            this.scrollToBottom();
            return;
        }

        // Обычное сообщение
        const isOwn = message.userId === this.currentUserId;
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${isOwn ? 'message-own' : ''} ${message.isInternal ? 'message-internal' : ''}`;
        messageDiv.dataset.commentId = message.id;

        const avatarHtml = message.authorAvatar
            ? `<img src="${message.authorAvatar}" alt="Avatar" class="avatar-small" />`
            : `<div class="avatar-small avatar-placeholder">${(message.authorName?.[0] || '?').toUpperCase()}</div>`;

        const attachmentsHtml = message.attachments && message.attachments.length > 0
            ? this.generateAttachmentsHtml(message.attachments)
            : '';

        const timeFormatted = this.formatTime(message.createdAt);

        const editedHtml = message.editedAt
            ? `<div class="message-edited">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor" style="margin-right: 4px; opacity: 0.6;">
                <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/>
            </svg>
            Изменено
        </div>`
            : '';

        messageDiv.innerHTML = `
        <div class="message-avatar">
            ${avatarHtml}
        </div>
        <div class="message-content">
            <div class="message-header">
                <div class="message-author-info">
                    <span class="message-author">${this.escapeHtml(message.authorName)}</span>
                    ${message.isInternal ? '<span class="badge-internal">Внутренний</span>' : ''}
                </div>
            </div>
            <div class="message-text">${this.escapeHtml(message.content)}</div>
            ${attachmentsHtml}
            ${editedHtml}
            <div class="message-footer">
                <span class="message-time local-time" data-time="${message.createdAt}">${timeFormatted}</span>
                ${isOwn ? `
                <button class="message-options-btn" title="Действия">
                    <svg viewBox="0 0 24 24" width="14" height="14">
                        <path fill="currentColor" d="M12,16A2,2 0 0,1 14,18A2,2 0 0,1 12,20A2,2 0 0,1 10,18A2,2 0 0,1 12,16M12,10A2,2 0 0,1 14,12A2,2 0 0,1 12,14A2,2 0 0,1 10,12A2,2 0 0,1 12,10M12,4A2,2 0 0,1 14,6A2,2 0 0,1 12,8A2,2 0 0,1 10,6A2,2 0 0,1 12,4Z" />
                    </svg>
                </button>` : ''}
            </div>
        </div>
    `;

        container.appendChild(messageDiv);
        this.processAttachmentsInElement(messageDiv);
        this.scrollToBottom();
    },

    // Форматирование времени для отображения
    formatTime: function (isoString) {
        if (!isoString) return '';

        const date = new Date(isoString);

        if (isNaN(date.getTime())) {
            console.error('Invalid date in formatTime:', isoString);
            return isoString;
        }

        const now = new Date();
        const yesterday = new Date();
        yesterday.setDate(yesterday.getDate() - 1);

        // Обнуляем время для корректного сравнения только дат
        const dateOnly = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        const nowOnly = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        const yesterdayOnly = new Date(yesterday.getFullYear(), yesterday.getMonth(), yesterday.getDate());

        const time = date.toLocaleTimeString('ru-RU', {
            hour: '2-digit',
            minute: '2-digit',
            hourCycle: 'h24'
        });

        // Сегодня
        if (dateOnly.getTime() === nowOnly.getTime()) {
            return time;
        }

        // Вчера
        if (dateOnly.getTime() === yesterdayOnly.getTime()) {
            return `Вчера, ${time}`;
        }

        // В этом году
        if (date.getFullYear() === now.getFullYear()) {
            return date.toLocaleDateString('ru-RU', {
                day: 'numeric',
                month: 'long'
            }) + `, ${time}`;
        }

        // Полная дата
        return date.toLocaleDateString('ru-RU', {
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        }) + `, ${time}`;
    },

    processAttachmentsInElement: function (element) {
        const attachmentCells = element.querySelectorAll('.AttachmentCell');
        attachmentCells.forEach(cell => {
            const fileName = cell.querySelector('.AttachmentCell__headline')?.textContent;
            if (!fileName) return;

            const ext = fileName.split('.').pop().toLowerCase();
            const isImage = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext);

            if (isImage) {
                const iconBlock = cell.querySelector('.AttachmentCell__imageBlock');
                if (iconBlock) {
                    const onClickAttr = cell.getAttribute('onclick');
                    if (onClickAttr && onClickAttr.includes("window.open")) {
                        const filePath = onClickAttr.match(/'([^']+)'/)[1];

                        iconBlock.innerHTML = '';
                        const img = document.createElement('img');
                        img.src = filePath;
                        img.style.width = '100%';
                        img.style.height = '100%';
                        img.style.objectFit = 'cover';
                        img.style.borderRadius = '4px';
                        img.loading = 'lazy';
                        iconBlock.appendChild(img);
                        iconBlock.classList.add('image-preview');

                        cell.removeAttribute('onclick');
                        cell.classList.add('AttachmentCell--clickable');
                        cell.addEventListener('click', () => {
                            window.FilePreviewHelper.openImageModal(filePath, fileName);
                        });
                    }
                }
            }
        });
    },

    updateMessage: function (data) {
        const messageDiv = document.querySelector(`[data-comment-id="${data.id}"]`);
        if (!messageDiv) return;

        // Обновляем текст
        const textDiv = messageDiv.querySelector('.message-text');
        if (textDiv) textDiv.textContent = data.content;

        // Обновляем вложения
        const attachmentsContainer = messageDiv.querySelector('.message-attachments');
        if (attachmentsContainer) {
            attachmentsContainer.remove();
        }

        if (data.attachments && data.attachments.length > 0) {
            const attachmentsHtml = this.generateAttachmentsHtml(data.attachments);
            const contentDiv = messageDiv.querySelector('.message-content');
            const editedDiv = messageDiv.querySelector('.message-edited');

            const newContainer = document.createElement('div');
            newContainer.className = 'message-attachments';
            newContainer.innerHTML = attachmentsHtml;

            contentDiv.insertBefore(newContainer, editedDiv || null);
            this.processAttachmentsInElement(newContainer);
        }

        // Обновляем метку "Изменено"
        let editedDiv = messageDiv.querySelector('.message-edited');
        if (!editedDiv) {
            editedDiv = document.createElement('div');
            editedDiv.className = 'message-edited';
            const contentDiv = messageDiv.querySelector('.message-content');
            if (contentDiv) {
                contentDiv.appendChild(editedDiv);
            }
        }

        editedDiv.innerHTML = `
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor" style="margin-right: 4px; opacity: 0.6;">
                <path d="M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z"/>
            </svg>
            Изменено
        `;
    },

    deleteMessage: function (commentId) {
        const messageDiv = document.querySelector(`[data-comment-id="${commentId}"]`);
        if (messageDiv) {
            messageDiv.remove();
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
        fetch(`/Ticket/GetComments?ticketId=${this.ticketId}`)
            .then(response => response.json())
            .then(comments => {
                const comment = comments.find(c => c.id === commentId);
                if (!comment) {
                    console.error('Комментарий не найден');
                    alert('Комментарий не найден');
                    return;
                }

                const contentInput = document.getElementById('comment-content');
                if (contentInput) contentInput.value = comment.content;

                this.pendingFiles = [];

                if (comment.attachments && comment.attachments.length > 0) {
                    comment.attachments.forEach(att => {
                        const virtualFile = {
                            id: Date.now() + Math.random(),
                            fileName: att.fileName,
                            path: att.filePath
                        };
                        this.pendingFiles.push(virtualFile);
                    });
                }

                this.renderPendingFiles();

                const form = document.getElementById('comment-form');
                if (form) form.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

                this.editingCommentId = commentId;
                this.originalContent = currentContent;
            })
            .catch(err => console.error('Ошибка получения комментария:', err));
    },

    removeVirtualFile: function (virtualFile) {
        this.pendingFiles = this.pendingFiles.filter(f => f.id !== virtualFile.id);

        const filePath = virtualFile.path || virtualFile.filePath;
        if (filePath) {
            this.removedAttachments.push(filePath);
        }

        this.renderPendingFiles();
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

// Обработка local-time элементов
document.addEventListener('DOMContentLoaded', function () {
    // Конвертируем все data-time в локальное время
    document.querySelectorAll('[data-time]').forEach(el => {
        const isoTime = el.getAttribute('data-time');
        if (isoTime) {
            const date = new Date(isoTime);
            if (!isNaN(date.getTime())) {
                const localTime = date.toLocaleTimeString('ru-RU', {
                    hour: '2-digit',
                    minute: '2-digit',
                    hourCycle: 'h24'
                });
                el.textContent = localTime;
            }
        }
    });
});