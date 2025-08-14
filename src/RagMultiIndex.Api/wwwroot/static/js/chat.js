/**
 * Chat functionality copied from original Python version.
 */
document.addEventListener('DOMContentLoaded', function() {
	const chatForm = document.getElementById('chat-form');
	const chatInput = document.getElementById('chat-input');
	const sendButton = document.getElementById('send-button');
	const chatHistory = document.getElementById('chat-history');
	const chatContainer = document.getElementById('chat-container');
	const loadingIndicator = document.getElementById('loading-indicator');
	const errorContainer = document.getElementById('error-container');
	const errorMessage = document.getElementById('error-message');

	let messages = [];

	chatForm.addEventListener('submit', handleChatSubmit);
	chatInput.addEventListener('keydown', handleKeyDown);

	function handleChatSubmit(e) {
		e.preventDefault();
		const query = chatInput.value.trim();
		if (query && !isLoading()) {
			sendMessage(query);
		}
	}
	function handleKeyDown(e) {
		if (e.key === 'Enter' && !e.shiftKey) {
			e.preventDefault();
			const query = chatInput.value.trim();
			if (query && !isLoading()) {
				sendMessage(query);
			}
		}
	}
	function isLoading() { return !loadingIndicator.classList.contains('d-none'); }
	function addUserMessage(text) {
		if (chatHistory.querySelector('.text-center')) { chatHistory.innerHTML = ''; }
		const wrapper = document.createElement('div');
		wrapper.className = 'd-flex mb-4 justify-content-end align-items-end';
		const card = document.createElement('div');
		card.className = 'card user-card';
		card.style.maxWidth = '80%';
		const cardBody = document.createElement('div');
		cardBody.className = 'card-body';
		const messageContent = document.createElement('div');
		messageContent.className = 'message-content';
		messageContent.style.lineHeight = '1.5';
		let htmlContent;
		try { marked.setOptions({ breaks: true, gfm: true, headerIds: false, mangle: false }); htmlContent = marked.parse(text); }
		catch { htmlContent = text.replace(/\n/g, '<br>'); }
		messageContent.innerHTML = htmlContent;
		cardBody.appendChild(messageContent); card.appendChild(cardBody); wrapper.appendChild(card);
		const avatar = document.createElement('div'); avatar.className = 'avatar-badge user-avatar-badge ms-2'; avatar.textContent = 'You';
		wrapper.appendChild(avatar); chatHistory.appendChild(wrapper); scrollToBottom();
	}
	function addAssistantMessage(content, citations) {
		const wrapper = document.createElement('div');
		wrapper.className = 'd-flex mb-4 align-items-start flex-nowrap';
		wrapper.style.width = '100%';
		const card = document.createElement('div'); card.className = 'card assistant-card'; card.style.maxWidth = '80%'; card.style.flexShrink = '1'; card.style.minWidth = '0';
		const cardBody = document.createElement('div'); cardBody.className = 'card-body';
		const messageContent = document.createElement('div'); messageContent.className = 'message-content'; messageContent.style.lineHeight = '1.5'; messageContent.style.wordBreak = 'break-word'; messageContent.style.overflowWrap = 'anywhere';
		let formattedContent = content || ''; const messageId = 'msg-' + Date.now(); const messageCitations = {};
		if (citations && citations.length > 0) {
			const pattern = /\[doc(\d+)\]/g;
			formattedContent = formattedContent.replace(pattern, (match, index) => {
				const idx = parseInt(index);
				if (idx > 0 && idx <= citations.length) {
					const citation = citations[idx - 1];
					const citationData = JSON.stringify({ title: citation.title || '', content: citation.content || '', filePath: citation.filePath || '', url: citation.url || '' });
					messageCitations[idx] = citationData;
					return `<a class="badge citation-badge rounded-pill" data-message-id="${messageId}" data-index="${idx}">${idx}</a>`;
				}
				return match;
			});
		}
		let htmlContent;
		try { marked.setOptions({ breaks: true, gfm: true, headerIds: false, mangle: false }); htmlContent = marked.parse(formattedContent); }
		catch { htmlContent = formattedContent.replace(/\n/g, '<br>'); }
		messageContent.innerHTML = htmlContent;
		cardBody.appendChild(messageContent); card.appendChild(cardBody); card.setAttribute('id', messageId); card.setAttribute('data-citations', JSON.stringify(messageCitations));
		const avatar = document.createElement('div'); avatar.className = 'avatar-badge assistant-avatar-badge me-2'; avatar.textContent = 'AI';
		wrapper.appendChild(avatar); wrapper.appendChild(card); chatHistory.appendChild(wrapper);
		setTimeout(() => { const badges = messageContent.querySelectorAll('.badge[data-index]'); badges.forEach(badge => { badge.addEventListener('click', function(e){ e.preventDefault(); e.stopPropagation(); const mid = this.getAttribute('data-message-id'); const idx = this.getAttribute('data-index'); const messageElement = document.getElementById(mid); const mc = JSON.parse(messageElement.getAttribute('data-citations') || '{}'); const citationData = JSON.parse(mc[idx]); showCitationModal(citationData); }); }); }, 100);
		scrollToBottom();
	}
	function showCitationModal(citationData) {
		const existingOverlay = document.querySelector('.citation-overlay'); if (existingOverlay) existingOverlay.remove();
		let formattedContent = citationData.content || 'No content available';
		formattedContent = formattedContent.replace(/\[([^\]]+)\]\([^\)]+\)/g, '$1');
		let htmlContent; try { marked.setOptions({ breaks: true, gfm: true, headerIds: false, mangle: false }); htmlContent = marked.parse(formattedContent); } catch { htmlContent = formattedContent.replace(/\n/g, '<br>'); }
		const overlay = document.createElement('div'); overlay.className = 'citation-overlay'; overlay.setAttribute('role','dialog'); overlay.setAttribute('aria-modal','true'); overlay.setAttribute('aria-labelledby','citation-modal-title');
		overlay.innerHTML = `\n            <div class="citation-modal">\n                <div class="citation-modal-header">\n                    <h5 class="citation-modal-title" id="citation-modal-title">${citationData.title || 'Citation'}</h5>\n                    <button type="button" class="citation-close-button" aria-label="Close">&times;</button>\n                </div>\n                <div class="citation-modal-body">\n                    <div class="citation-content markdown-content">${htmlContent}</div>\n                    ${citationData.filePath ? `<div class="citation-source mt-3"><strong>Source:</strong> ${citationData.filePath}</div>` : ''}\n                    ${citationData.url ? `<div class="citation-url mt-2"><strong>URL:</strong> <a href="${citationData.url}" target="_blank" rel="noopener noreferrer">${citationData.url}</a></div>` : ''}\n                </div>\n            </div>`;
		document.body.appendChild(overlay); const modal = overlay.querySelector('.citation-modal'); modal.focus();
		const closeButton = overlay.querySelector('.citation-close-button'); closeButton.addEventListener('click', () => overlay.remove());
		overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
		document.addEventListener('keydown', function closeOnEscape(e){ if (e.key === 'Escape') { overlay.remove(); document.removeEventListener('keydown', closeOnEscape);} });
	}
	function showError(text) { errorMessage.textContent = text; errorContainer.classList.remove('d-none'); }
	function hideError() { errorContainer.classList.add('d-none'); }
	function showLoading() { loadingIndicator.classList.remove('d-none'); sendButton.disabled = true; }
	function hideLoading() { loadingIndicator.classList.add('d-none'); sendButton.disabled = false; }
	function scrollToBottom() { setTimeout(() => { chatContainer.scrollTop = chatContainer.scrollHeight; }, 50); }
	function sendMessage(text) {
		hideError(); addUserMessage(text); chatInput.value='';
		messages.push({ role: 'user', content: text }); showLoading();
		fetch('/api/chat/completion', { method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify({ messages }) })
			.then(response => { if(!response.ok){ return response.json().then(err=>{ throw new Error(err.message || `HTTP error! Status: ${response.status}`); }).catch(()=>{ throw new Error(`HTTP error! Status: ${response.status}`); }); } return response.json(); })
			.then(data => { hideLoading(); if (data.error) { showError(data.message || 'An error occurred'); return; } const choice = data.choices && data.choices.length>0 ? data.choices[0] : null; if(!choice || !choice.message || !choice.message.content){ showError('No answer received from the AI service.'); return; } const message = choice.message; const content = message.content; const citations = message.context?.citations || []; addAssistantMessage(content, citations); messages.push({ role: 'assistant', content }); })
			.catch(error => { hideLoading(); showError(`Error: ${error.message}`); console.error('Error:', error); });
	}
});
