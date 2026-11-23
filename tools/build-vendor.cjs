const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const destination = path.join(root, 'ADSync.Client/wwwroot/lib/vue');
fs.mkdirSync(destination, { recursive: true });
for (const [source, target] of [['dist/vue.esm-browser.prod.js', 'vue.esm-browser.prod.js'], ['LICENSE', 'LICENSE']]) {
  fs.copyFileSync(path.join(root, 'node_modules/vue', source), path.join(destination, target));
}
console.log('Vue.js copiado para os recursos locais do cliente.');
