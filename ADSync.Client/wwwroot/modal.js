const dialogs = new WeakMap();

export function open(dialog, receiver, busy) {
    let state = dialogs.get(dialog);
    if (!state) {
        state = { receiver, busy, trigger: document.activeElement };
        dialogs.set(dialog, state);
        dialog.addEventListener('cancel', event => {
            event.preventDefault();
            if (!state.busy) state.receiver.invokeMethodAsync('RequestClose');
        });
        dialog.addEventListener('click', event => {
            if (event.target !== dialog || state.busy) return;
            const bounds = dialog.getBoundingClientRect();
            if (event.clientX < bounds.left || event.clientX > bounds.right ||
                event.clientY < bounds.top || event.clientY > bounds.bottom) {
                state.receiver.invokeMethodAsync('RequestClose');
            }
        });
        dialog.addEventListener('close', () => {
            if (state.trigger?.isConnected) state.trigger.focus({ preventScroll: true });
        });
    }
    state.busy = busy;
    if (!dialog.open) dialog.showModal();
}

export function close(dialog) {
    if (dialog?.open) dialog.close();
}
