// Print a separate document so navigation, session controls and form fields stay out of the PDF.
export async function printReport(element) {
    if (!element) throw new Error('Relatório indisponível.');
    const frame = document.createElement('iframe');
    frame.title = 'Impressão do relatório mensal';
    frame.setAttribute('aria-hidden', 'true');
    Object.assign(frame.style, { position: 'fixed', left: '-10000px', width: '1100px', height: '800px', border: '0' });
    document.body.appendChild(frame);
    try {
        const doc = frame.contentDocument;
        doc.documentElement.lang = 'pt-BR';
        const title = doc.createElement('title');
        title.textContent = 'ADSync - Relatório mensal';
        doc.head.appendChild(title);
        const style = doc.createElement('style');
        style.textContent = '@page { size: A4; margin: 16mm; } body { margin: 0; font-family: "Segoe UI", Arial, sans-serif; color: #203d36; }';
        doc.head.appendChild(style);
        const loaded = new Promise((resolve, reject) => {
            const link = doc.createElement('link');
            link.rel = 'stylesheet';
            link.href = new URL('ADSync.Client.styles.css', document.baseURI).href;
            link.onload = resolve;
            link.onerror = () => reject(new Error('Não foi possível carregar os estilos do relatório.'));
            doc.head.appendChild(link);
        });
        const copy = element.cloneNode(true);
        copy.querySelectorAll('.report-no-print').forEach(node => node.remove());
        doc.body.appendChild(copy);
        await loaded;
        await doc.fonts.ready;
        frame.contentWindow.addEventListener('afterprint', () => frame.remove(), { once: true });
        frame.contentWindow.focus();
        frame.contentWindow.print();
        // Some browsers do not dispatch afterprint for a cancelled dialog.
        setTimeout(() => frame.remove(), 60000);
    } catch (error) {
        frame.remove();
        throw error;
    }
}
