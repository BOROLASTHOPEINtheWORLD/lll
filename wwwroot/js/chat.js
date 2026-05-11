window.ChatHelper = {
    connection: null,
    ticketId: null,
    currentUserId: null,
    pendingFiles: [],
    removedAttachments: [],

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
    },

    processExistingAttachments: function () {
        // Находим все вложения в чате
        const existingAttachments = document.querySelectorAll('.message-attachments .AttachmentCell');
        existingAttachments.forEach(cell => {
            const fileName = cell.querySelector('.AttachmentCell__headline')?.textContent;
            if (!fileName) return;

            const ext = fileName.split('.').pop().toLowerCase();
            const isImage = ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp'].includes(ext);

            if (isImage) {
                const iconBlock = cell.querySelector('.AttachmentCell__imageBlock');
                if (iconBlock) {
                    // Если это img, то уже обработано через Razor
                    if (iconBlock.querySelector('img')) {
                        // Убираем onclick и добавляем клик на открытие модального окна
                        const filePath = cell.dataset.filePath || '';
                        cell.classList.add('AttachmentCell--clickable');
                        cell.addEventListener('click', (e) => {
                            if (e.target.closest('.AttachmentCell__remove')) return;
                            window.FilePreviewHelper.openImageModal(filePath, fileName);
                        });
                    } else {
                        // Старая логика для совместимости
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
            } else {
                // Для не-изображений тоже добавляем обработчик клика
                const filePath = cell.dataset.filePath || '';
                if (filePath) {
                    cell.classList.add('AttachmentCell--clickable');
                    cell.addEventListener('click', (e) => {
                        if (e.target.closest('.AttachmentCell__remove')) return;
                        window.open(filePath, '_blank');
                    });
                }
            }
        });
    },

    initCommentForm: function () {
        const form = document.getElementById('comment-form');
        const fileInput = document.getElementById('comment-file-input');
        const attachBtn = document.getElementById('comment-attach-btn');

        // +++ ДОБАВЛЯЕМ ЭТОТ КОД +++
        const contentInput = document.getElementById('comment-content');

        // Обработка отправки по нажатию Enter
        if (contentInput) {
            contentInput.addEventListener('keydown', (e) => {
                // Отправляем по Enter (но не при Shift+Enter)
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault(); // Предотвращаем перенос строки

                    // Проверяем, не пустое ли сообщение
                    const content = contentInput.value.trim();
                    const hasFiles = this.pendingFiles.length > 0;

                    if (content || hasFiles) {
                        // Имитируем отправку формы
                        form.dispatchEvent(new Event('submit'));
                    }
                }
            });
        }

        // Удаляем старые обработчики, если они были
        if (fileInput._initialized) {
            fileInput.removeEventListener('change', fileInput._changeHandler);
        }

        if (attachBtn && fileInput) {
            // Убираем старый click-обработчик кнопки, если был
            if (fileInput._clickHandler) {
                attachBtn.removeEventListener('click', fileInput._clickHandler);
            }

            // Создаём новый обработчик
            const changeHandler = (e) => {
                const files = Array.from(e.target.files);
                files.forEach(file => this.addPendingFile(file));
                fileInput.value = ''; // очистка input после выбора
            };

            const clickHandler = () => fileInput.click();

            // Сохраняем ссылки на обработчики, чтобы можно было удалить
            fileInput._changeHandler = changeHandler;
            fileInput._clickHandler = clickHandler;

            fileInput.addEventListener('change', changeHandler);
            attachBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                fileInput.click();
            });

            fileInput._initialized = true;
        }

        if (form) {
            form.removeEventListener('submit', this.handleSubmit.bind(this)); // удаляем старый, если был
            form.addEventListener('submit', (e) => this.handleSubmit(e));
        }
    },

    addPendingFile: function (file) {
        // Добавляем уникальный ID для новых файлов
        if (!file.id) {
            file.id = Date.now() + Math.random();
        }

        this.pendingFiles.push(file);
        this.renderPendingFiles();
    },

    removePendingFile: function (file) { // ← Принимаем сам файл, а не индекс
        this.pendingFiles = this.pendingFiles.filter(f => f.id !== file.id);

        // Удаляем элемент из DOM
        const previewItems = document.querySelectorAll('#comment-file-preview .AttachmentCell');
        for (let item of previewItems) {
            if (item.dataset.fileId == file.id) {
                item.remove();
                break;
            }
        }
    },

    renderPendingFiles: function () {
        const container = document.getElementById('comment-file-preview');
        if (!container) return;

        container.innerHTML = '';
        this.pendingFiles.forEach((file, index) => {
            let preview;

            // Теперь правильно распознаем виртуальные файлы по наличию поля path
            if (file.path) {
                preview = FilePreviewHelper.createVirtualFilePreview(
                    {
                        fileName: file.fileName,
                        filePath: file.path // ← Передаем path как filePath в хелпер
                    },
                    () => this.removeVirtualFile(file),
                    true
                );
            } else {
                preview = FilePreviewHelper.createFilePreview(
                    file,
                    () => this.removePendingFile(file), // ← Исправлено: передаем file вместо index
                    true
                );
            }

            container.appendChild(preview);
        });
    },

    // +++ МЕТОД ДЛЯ ПОЛНОГО СБРОСА ФОРМЫ
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

        // Проверяем: если нет контента И нет файлов — не отправляем
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

            // Новые файлы
            this.pendingFiles.forEach(file => {
                if (file.path) {
                    // Теперь это правильно распознается как виртуальный файл
                    formData.append('existingAttachments', file.path);
                } else {
                    formData.append('attachments', file);
                }
            });

            // Удалённые файлы
            this.removedAttachments.forEach(path => {
                formData.append('removedAttachments', path);
            });

            let response;

            if (this.editingCommentId) {
                // Редактирование
                formData.append('commentId', this.editingCommentId);
                response = await fetch('/Ticket/EditComment', {
                    method: 'POST',
                    body: formData
                });
            } else {
                // Новый коммент
                response = await fetch('/Ticket/AddComment', {
                    method: 'POST',
                    body: formData
                });
            }

            if (!response.ok) {
                const text = await response.text();
                throw new Error(`Ошибка сервера: ${response.status}. ${text}`);
            }

            // Успех - очищаем форму
            if (contentInput) contentInput.value = '';

            // +++ ПОЛНЫЙ СБРОС ФОРМЫ ПОСЛЕ УСПЕШНОЙ ОТПРАВКИ +++
            this.resetCommentForm();

            console.log('Comment sent via AJAX, waiting for SignalR broadcast...');
        } catch (err) {
            console.error('Error sending message:', err);
            this.showError(`Ошибка при отправке: ${err.message || err}`);
        }
    },

    // +++ Добавляем метод для красивой ошибки
    showError: function (message) {
        // Проверим, есть ли уже контейнер ошибки
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
            // Вставляем перед формой
            const form = document.getElementById('comment-form');
            if (form) form.parentNode.insertBefore(errorEl, form);
        }

        errorEl.textContent = message;
        errorEl.style.display = 'block';

        // Автоматически скрываем через 5 секунд
        setTimeout(() => {
            if (errorEl) errorEl.style.display = 'none';
        }, 5000);
    },

    // +++ Вспомогательный метод для генерации HTML вложений +++
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
                    <svg aria-hidden="true" display="block" class="vkuiIcon vkuiIcon--24 vkuiIcon--w-24 vkuiIcon--h-24 vkuiIcon--document_list_outline_24" 
                         width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M7.996 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8zM13 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8z"></path>
                        <path fill-rule="evenodd" d="M12.473 2c.3 0 .586 0 .866.066a2.4 2.4 0 0 1 .694.288c.245.15.447.353.659.565l4.389 4.39c.212.21.415.413.565.658a2.4 2.4 0 0 1 .288.694c.067.28.066.566.066.866v6.012c0 .947 0 1.713-.05 2.333-.053.64-.163 1.203-.43 1.726a4.4 4.4 0 0 1-1.922 1.922c-.523.267-1.087.377-1.726.43-.62.05-1.386.05-2.334.05h-3.076c-.948 0-1.714 0-2.334-.05-.64-.053-1.203-.163-1.726-.43a4.4 4.4 0 0 1-1.922-1.922c-.267-.523-.377-1.087-.43-1.726C4 17.252 4 16.486 4 15.538V8.462c0-.948 0-1.714.05-2.334.053-.64.163-1.203.43-1.726A4.4 4.4 0 0 1 6.402 2.48c.523-.267 1.087-.377 1.726-.43C8.748 2 9.514 2 10.462 2zM10.5 3.8H12v2.135c0 .53 0 .981.03 1.352.032.39.102.768.286 1.13a2.9 2.9 0 0 0 1.267 1.267c.362.184.741.254 1.13.286.37.03.822.03 1.351.03H18.2v5.5c0 .995 0 1.687-.045 2.226-.043.527-.123.828-.238 1.054a2.6 2.6 0 0 1-1.137 1.137c-.226.115-.527.195-1.055.238-.538.044-1.23.045-2.225.045h-3c-.995 0-1.687 0-2.226-.045-.527-.043-.828-.123-1.054-.238a2.6 2.6 0 0 1-1.137-1.137c-.115-.226-.195-.527-.238-1.055-.044-.538-.045-1.23-.045-2.225v-7c0-.995 0-1.687.045-2.225.043-.528.123-.829.238-1.055A2.6 2.6 0 0 1 7.22 4.083c.226-.115.527-.195 1.054-.238C8.813 3.8 9.505 3.8 10.5 3.8m3.3.773L17.427 8.2H16.1c-.575 0-.952 0-1.24-.024-.278-.023-.393-.062-.46-.096a1.1 1.1 0 0 1-.48-.48c-.034-.066-.073-.182-.096-.46A17 17 0 0 1 13.8 5.9z" clip-rule="evenodd"></path>
                    </svg>`;
            }

            imageBlockContainer.appendChild(imageBlock);

            // Info block
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
            footnote.textContent = `${ext.toUpperCase()} ᐧ Загружен`;

            infoBlock.appendChild(headline);
            infoBlock.appendChild(footnote);
            infoBlockContainer.appendChild(infoBlock);

            attachmentDiv.appendChild(imageBlockContainer);
            attachmentDiv.appendChild(infoBlockContainer);

            // Клик по изображению — открывает модальное окно
            if (isImage) {
                attachmentDiv.addEventListener('click', () => {
                    window.FilePreviewHelper.openImageModal(attachment.filePath, attachment.fileName);
                });
            } else {
                // Для файлов — просто открываем в новом окне
                attachmentDiv.addEventListener('click', () => {
                    window.open(attachment.filePath, '_blank');
                });
            }

            container.appendChild(attachmentDiv);
        });

        return container.innerHTML;
    },

    // +++ Вспомогательный метод для перепроцессинга вложений +++
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

                        // Заменяем содержимое на изображение
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

                        // Убираем onclick и добавляем обработчик
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

    appendMessage: function (message) {
        const container = document.getElementById('chat-messages');
        if (!container) return;

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
            ? this.generateAttachmentsHtml(message.attachments)
            : '';

        // +++ Конвертируем время на клиенте
        const createdAtLocal = message.createdAt
            ? new Date(message.createdAt).toLocaleString('ru-RU', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
                hourCycle: 'h24'
            })
            : '';

        const editedHtml = message.editedAt
            ? `<div class="message-edited">Отредактировано ${new Date(message.editedAt).toLocaleString('ru-RU', {
                day: '2-digit',
                month: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                hourCycle: 'h24'
            })}</div>`
            : '';

        messageDiv.innerHTML = `
        <div class="message-avatar">
            ${avatarHtml}
        </div>
        <div class="message-content">
            <div class="message-header">
                <span class="message-author">${this.escapeHtml(message.authorName)}</span>
                <span class="message-time">${createdAtLocal}</span>
                ${message.isInternal ? '<span class="badge-internal">Внутренний</span>' : ''}
                <!-- УБРАЛИ actionsHtml -->
            </div>
            <div class="message-text">${this.escapeHtml(message.content)}</div>
            ${attachmentsHtml}
            ${editedHtml}
        </div>
    `;

        container.appendChild(messageDiv);

        // +++ Обрабатываем вложения после добавления сообщения
        this.processAttachmentsInElement(messageDiv);

        this.scrollToBottom();

        const counter = document.getElementById('comments-count');
        if (counter) {
            const count = parseInt(counter.textContent) || 0;
            counter.textContent = `${count + 1} сообщений`;
        }
    },

    updateMessage: function (data) {
        const messageDiv = document.querySelector(`[data-comment-id="${data.id}"]`);
        if (!messageDiv) return;

        // Обновляем текст
        const textDiv = messageDiv.querySelector('.message-text');
        if (textDiv) textDiv.textContent = data.content;

        // +++ ОБНОВЛЯЕМ ВЛОЖЕНИЯ +++
        const attachmentsContainer = messageDiv.querySelector('.message-attachments');

        // Удаляем существующие вложения
        if (attachmentsContainer) {
            attachmentsContainer.remove();
        }

        // Добавляем новые вложения, если есть
        if (data.attachments && data.attachments.length > 0) {
            const attachmentsHtml = this.generateAttachmentsHtml(data.attachments);
            const contentDiv = messageDiv.querySelector('.message-content');
            const editedDiv = messageDiv.querySelector('.message-edited');

            const newContainer = document.createElement('div');
            newContainer.className = 'message-attachments';
            newContainer.innerHTML = attachmentsHtml;

            // Вставляем перед меткой "Отредактировано" или в конец contentDiv
            contentDiv.insertBefore(newContainer, editedDiv || null);

            // Перепроцессим изображения (добавляем обработчики кликов и т.д.)
            this.processAttachmentsInElement(newContainer);
        }

        // Обновляем метку "Отредактировано"
        let editedDiv = messageDiv.querySelector('.message-edited');
        if (!editedDiv) {
            editedDiv = document.createElement('div');
            editedDiv.className = 'message-edited';
            messageDiv.querySelector('.message-content').appendChild(editedDiv);
        }

        const editedLocal = new Date(data.editedAt).toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            hourCycle: 'h24'
        });
        editedDiv.textContent = `Отредактировано ${editedLocal}`;
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
        // НЕ используем prompt — получаем с сервера полный комментарий (включая вложения)
        fetch(`/Ticket/GetComments?ticketId=${this.ticketId}`)
            .then(response => response.json())
            .then(comments => {
                const comment = comments.find(c => c.id === commentId);
                if (!comment) {
                    console.error('Комментарий не найден');
                    alert('Комментарий не найден');
                    return;
                }

                // Заполняем текст
                const contentInput = document.getElementById('comment-content');
                if (contentInput) contentInput.value = comment.content;

                // Если есть вложения — восстанавливаем их (НЕ очищая предыдущие)
                if (comment.attachments && comment.attachments.length > 0) {
                    // Загружаем файлы обратно (как "виртуальные" объекты)
                    comment.attachments.forEach(att => {
                        // Проверяем, не добавлен ли уже такой файл
                        const exists = this.pendingFiles.some(f => f.path === att.filePath);
                        if (exists) return;

                        const virtualFile = {
                            id: Date.now() + Math.random(),
                            fileName: att.fileName,
                            path: att.filePath // ← Ключевое исправление: используем path вместо filePath
                        };

                        // Создаём превью как будто это виртуальный файл
                        const preview = FilePreviewHelper.createVirtualFilePreview(
                            virtualFile,
                            () => this.removeVirtualFile(virtualFile),
                            true
                        );

                        // Добавляем в список
                        this.pendingFiles.push(virtualFile);
                        const previewContainer = document.getElementById('comment-file-preview');
                        if (previewContainer) previewContainer.appendChild(preview);
                    });
                }

                // Прокручиваем к полю ввода
                const form = document.getElementById('comment-form');
                if (form) form.scrollIntoView({ behavior: 'smooth', block: 'nearest' });

                // Сохраняем ID редактируемого комментария
                this.editingCommentId = commentId;
                this.originalContent = currentContent;
            })
            .catch(err => console.error('Ошибка получения комментария:', err));
    },

    // +++ Метод для удаления виртуальных файлов
    removeVirtualFile: function (virtualFile) {
        // Удаляем из массива
        this.pendingFiles = this.pendingFiles.filter(f => f.id !== virtualFile.id);

        // Добавляем в список удалённых
        this.removedAttachments.push(virtualFile.path);

        // Находим элемент в DOM и удаляем его напрямую
        const previewItems = document.querySelectorAll('#comment-file-preview .AttachmentCell');
        for (let item of previewItems) {
            if (item.dataset.virtualFileId == virtualFile.id) {
                item.remove();
                break;
            }
        }
    },

    // +++ Добавим ID редактируемого комментария
    editingCommentId: null,
    originalContent: '',

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

document.addEventListener('DOMContentLoaded', function () {
    // Находим все элементы, у которых есть атрибут data-time
    document.querySelectorAll('[data-time]').forEach(el => {
        const isoTime = el.getAttribute('data-time');
        if (isoTime) {
            const date = new Date(isoTime);
            // Конвертируем в локальное время браузера
            const localTime = date.toLocaleString('ru-RU', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
                hourCycle: 'h24'
            });
            el.textContent = localTime;
        }
    });

    // +++ Добавляем CSS для предотвращения редактирования текста в сообщениях
    const style = document.createElement('style');
    style.textContent = `
        .message-text {
            -webkit-user-select: text;
            -moz-user-select: text;
            -ms-user-select: text;
            user-select: text;
            cursor: text;
        }
        
        .message-text::selection {
            background: #b3d4fc;
        }
    `;
    document.head.appendChild(style);
});