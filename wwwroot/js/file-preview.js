// Общие функции для работы с файлами
window.FilePreviewHelper = {
    formatFileSize: function (bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    },

    getFileIcon: function () {
        return `<svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" style="color: #8a8a9e;">
          <path d="M12.473 2c.3 0 .586 0 .866.066a2.4 2.4 0 0 1 .694.288c.245.15.447.353.659.565l4.389 4.39c.212.21.415.413.565.658a2.4 2.4 0 0 1 .288.694c.067.28.066.566.066.866v6.012c0 .947 0 1.713-.05 2.333-.053.64-.163 1.203-.43 1.726a4.4 4.4 0 0 1-1.922 1.922c-.523.267-1.087.377-1.726.43-.62.05-1.386.05-2.334.05h-3.076c-.948 0-1.714 0-2.334-.05-.64-.053-1.203-.163-1.726-.43a4.4 4.4 0 0 1-1.922-1.922c-.267-.523-.377-1.087-.43-1.726C4 17.252 4 16.486 4 15.538V8.462c0-.948 0-1.714.05-2.334.053-.64.163-1.203.43-1.726A4.4 4.4 0 0 1 6.402 2.48c.523-.267 1.087-.377 1.726-.43C8.748 2 9.514 2 10.462 2zM10.5 3.8H12v2.135c0 .53 0 .981.03 1.352.032.39.102.768.286 1.13a2.9 2.9 0 0 0 1.267 1.267c.362.184.741.254 1.13.286.37.03.822.03 1.351.03H18.2v5.5c0 .995 0 1.687-.045 2.226-.043.527-.123.828-.238 1.054a2.6 2.6 0 0 1-1.137 1.137c-.226.115-.527.195-1.055.238-.538.044-1.23.045-2.225.045h-3c-.995 0-1.687 0-2.226-.045-.527-.043-.828-.123-1.054-.238a2.6 2.6 0 0 1-1.137-1.137c-.115-.226-.195-.527-.238-1.055-.044-.538-.045-1.23-.045-2.225v-7c0-.995 0-1.687.045-2.225.043-.528.123-.829.238-1.055A2.6 2.6 0 0 1 7.22 4.083c.226-.115.527-.195 1.054-.238C8.813 3.8 9.505 3.8 10.5 3.8m3.3.773L17.427 8.2H16.1c-.575 0-.952 0-1.24-.024-.278-.023-.393-.062-.46-.096a1.1 1.1 0 0 1-.48-.48c-.034-.066-.073-.182-.096-.46A17 17 0 0 1 13.8 5.9z"/>
        </svg>`;
    },

    createFilePreview(file, onRemove, canClickToPreview = true) {
        const previewItem = document.createElement('div');
        previewItem.className = 'AttachmentCell';
        if (canClickToPreview) previewItem.classList.add('AttachmentCell--clickable');

        // --- Image block container ---
        const imageBlockContainer = document.createElement('div');
        imageBlockContainer.className = 'AttachmentCell__imageBlockContainer';

        const bg = document.createElement('div');
        bg.className = 'AttachmentCell__imageBlockBackground';
        imageBlockContainer.appendChild(bg);

        const imageBlock = document.createElement('div');
        imageBlock.className = 'AttachmentCell__imageBlock';

        if (file.type.startsWith('image/')) {
            imageBlock.classList.add('image');
            imageBlock.style.backgroundImage = `url(${URL.createObjectURL(file)})`;
            // Не добавляем SVG — только фон
        } else {
            // Иконка документа (точная копия из VK)
            imageBlock.innerHTML = `
            <svg aria-hidden="true" display="block" class="vkuiIcon vkuiIcon--24 vkuiIcon--w-24 vkuiIcon--h-24 vkuiIcon--document_list_outline_24" 
                 width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                <path d="M7.996 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8zM13 13.901a.9.9 0 0 1 .9-.9h1.2a.9.9 0 0 1 0 1.8h-1.2a.9.9 0 0 1-.9-.9m.9 2.297a.9.9 0 1 0 0 1.8h1.2a.9.9 0 0 0 0-1.8z"></path>
                <path fill-rule="evenodd" d="M12.473 2c.3 0 .586 0 .866.066a2.4 2.4 0 0 1 .694.288c.245.15.447.353.659.565l4.389 4.39c.212.21.415.413.565.658a2.4 2.4 0 0 1 .288.694c.067.28.066.566.066.866v6.012c0 .947 0 1.713-.05 2.333-.053.64-.163 1.203-.43 1.726a4.4 4.4 0 0 1-1.922 1.922c-.523.267-1.087.377-1.726.43-.62.05-1.386.05-2.334.05h-3.076c-.948 0-1.714 0-2.334-.05-.64-.053-1.203-.163-1.726-.43a4.4 4.4 0 0 1-1.922-1.922c-.267-.523-.377-1.087-.43-1.726C4 17.252 4 16.486 4 15.538V8.462c0-.948 0-1.714.05-2.334.053-.64.163-1.203.43-1.726A4.4 4.4 0 0 1 6.402 2.48c.523-.267 1.087-.377 1.726-.43C8.748 2 9.514 2 10.462 2zM10.5 3.8H12v2.135c0 .53 0 .981.03 1.352.032.39.102.768.286 1.13a2.9 2.9 0 0 0 1.267 1.267c.362.184.741.254 1.13.286.37.03.822.03 1.351.03H18.2v5.5c0 .995 0 1.687-.045 2.226-.043.527-.123.828-.238 1.054a2.6 2.6 0 0 1-1.137 1.137c-.226.115-.527.195-1.055.238-.538.044-1.23.045-2.225.045h-3c-.995 0-1.687 0-2.226-.045-.527-.043-.828-.123-1.054-.238a2.6 2.6 0 0 1-1.137-1.137c-.115-.226-.195-.527-.238-1.055-.044-.538-.045-1.23-.045-2.225v-7c0-.995 0-1.687.045-2.225.043-.528.123-.829.238-1.055A2.6 2.6 0 0 1 7.22 4.083c.226-.115.527-.195 1.054-.238C8.813 3.8 9.505 3.8 10.5 3.8m3.3.773L17.427 8.2H16.1c-.575 0-.952 0-1.24-.024-.278-.023-.393-.062-.46-.096a1.1 1.1 0 0 1-.48-.48c-.034-.066-.073-.182-.096-.46A17 17 0 0 1 13.8 5.9z" clip-rule="evenodd"></path>
            </svg>
        `;
        }

        imageBlockContainer.appendChild(imageBlock);

        // --- Info block ---
        const infoBlockContainer = document.createElement('div');
        infoBlockContainer.className = 'AttachmentCell__infoBlockContainer';

        const infoBlock = document.createElement('div');
        infoBlock.className = 'AttachmentCell__infoBlock';

        const headline = document.createElement('h4');
        headline.className = 'AttachmentCell__headline';
        const displayName = file.name.length > 25 ? file.name.substring(0, 22) + '...' : file.name;
        headline.textContent = displayName;
        headline.title = file.name;

        const footnote = document.createElement('span');
        footnote.className = 'AttachmentCell__footnote';
        const ext = file.name.split('.').pop().toUpperCase();
        footnote.textContent = `${ext} ᐧ ${this.formatFileSize(file.size)}`; // ← символ U+1403 (WebKit/Chrome поддерживают)

        infoBlock.appendChild(headline);
        infoBlock.appendChild(footnote);
        infoBlockContainer.appendChild(infoBlock);

        // --- Собрать всё ---
        previewItem.appendChild(imageBlockContainer);
        previewItem.appendChild(infoBlockContainer);

        // --- Кликабельность ---
        if (canClickToPreview) {
            previewItem.addEventListener('click', (e) => {
                if (e.target.closest('.AttachmentCell__remove')) return;
                if (file.type.startsWith('image/') || file.type === 'application/pdf') {
                    // Открыть в новой вкладке или модалке
                    const url = URL.createObjectURL(file);
                    window.open(url, '_blank');
                } else {
                    // Скачать
                    const a = document.createElement('a');
                    a.href = URL.createObjectURL(file);
                    a.download = file.name;
                    document.body.appendChild(a);
                    a.click();
                    document.body.removeChild(a);
                }
            });
        }

        // --- Кнопка удаления (если нужно) ---
        if (onRemove) {
            const removeBtn = document.createElement('button');
            removeBtn.className = 'AttachmentCell__remove';
            removeBtn.innerHTML = `
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M18 6L6 18M6 6l12 12"/>
            </svg>`;
            removeBtn.onclick = (e) => {
                e.stopPropagation();
                onRemove(file);
                previewItem.remove();
            };
            previewItem.appendChild(removeBtn);
        }

        return previewItem;
    },

    // Модальное окно для просмотра изображений
    openImageModal: function (imageUrl, imageName) {
        let modal = document.getElementById('imageModal');
        if (!modal) {
            const modalHtml = `
                <div id="imageModal" class="image-modal" style="display: none;">
                    <div class="image-modal-overlay"></div>
                    <div class="image-modal-container">
                        <button class="image-modal-close">&times;</button>
                        <img id="modalImage" src="" alt="Preview">
                        <div class="image-modal-caption">
                            <span id="modalCaption"></span>
                        </div>
                    </div>
                </div>
            `;
            document.body.insertAdjacentHTML('beforeend', modalHtml);
            modal = document.getElementById('imageModal');
            modal.querySelector('.image-modal-close').addEventListener('click', () => this.closeImageModal());
            modal.querySelector('.image-modal-overlay').addEventListener('click', () => this.closeImageModal());
        }

        const modalImg = document.getElementById('modalImage');
        const modalCaption = document.getElementById('modalCaption');

        if (modal && modalImg) {
            modal.style.display = 'flex';
            modalImg.src = imageUrl;
            if (modalCaption) modalCaption.textContent = imageName;
            document.body.style.overflow = 'hidden';
        }
    },

    closeImageModal: function () {
        const modal = document.getElementById('imageModal');
        if (modal) {
            modal.style.display = 'none';
            document.body.style.overflow = '';
        }
    }
};

document.addEventListener('DOMContentLoaded', function () {
    const modal = document.getElementById('imageModal');
    if (modal) {
        modal.addEventListener('click', function (e) {
            if (e.target === modal || e.target.classList.contains('image-modal-overlay') || e.target.classList.contains('image-modal-close')) {
                window.FilePreviewHelper.closeImageModal();
            }
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && modal.style.display === 'flex') {
                window.FilePreviewHelper.closeImageModal();
            }
        });
    }
});