import { createApp, reactive, ref, computed } from './lib/vue/vue.esm-browser.prod.js';

const applications = new WeakMap();
const normalize = value => String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLocaleLowerCase('pt-BR').trim();
const matches = (query, ...values) => values.some(value => normalize(value).includes(normalize(query)));
const byName = (a, b) => a.nome.localeCompare(b.nome, 'pt-BR');
const searchIcon = '<svg class="app-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg>';
const arrowIcon = '<svg class="app-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6"/></svg>';

// Vue controla apenas este host; os modais e a sessão permanecem no Blazor.
const template = `
<section class="inventory-section" aria-labelledby="inventory-equipment-title">
  <header class="inventory-section-heading">
    <div><h2 id="inventory-equipment-title">Equipamentos <span class="inventory-count">{{ data.equipment.length }}</span></h2><p>Identificação, situação e histórico de manutenção.</p></div>
    <div class="inventory-filters">
      <label class="inventory-search" for="inventory-equipment-search">Buscar equipamento
        <span>${searchIcon}<input id="inventory-equipment-search" type="search" placeholder="Nome, patrimônio ou setor" v-model="equipmentSearch" /></span>
      </label>
      <label class="inventory-situation" for="inventory-equipment-status">Situação da máquina
        <select id="inventory-equipment-status" aria-label="Situação da máquina" v-model="equipmentStatus"><option value="">Todas</option><option value="active">Ativas</option><option v-if="data.canSeeInactive" value="inactive">Inativas</option></select>
      </label>
    </div>
  </header>
  <div class="inventory-results"><p role="status" aria-live="polite" aria-atomic="true">{{ filteredEquipment.length }} de {{ data.equipment.length }} equipamento(s)</p><button v-if="equipmentSearch || equipmentStatus" type="button" class="inventory-clear-button" @click="clearEquipmentFilters">Limpar filtros</button></div>
  <p class="inventory-scroll-hint">Deslize a tabela para ver todas as colunas →</p>
  <div class="inventory-table-scroll" role="region" aria-labelledby="inventory-equipment-title" tabindex="0">
    <table class="inventory-table inventory-equipment-table">
      <thead><tr><th scope="col">Patrimônio</th><th scope="col">Equipamento</th><th scope="col">Setor</th><th scope="col">Situação</th><th scope="col">Histórico de O.S.</th></tr></thead>
      <tbody>
        <tr v-for="item in filteredEquipment" :key="item.id">
          <td class="inventory-code">{{ item.codigoPatrimonio }}</td><th scope="row">{{ item.nome }}</th><td>{{ item.setor }}</td>
          <td><span class="inventory-tag" :class="item.ativo ? 'is-active' : 'is-inactive'">{{ item.ativo ? 'Ativa' : 'Inativa' }}</span></td>
          <td><button v-if="item.historyCount > 0" type="button" class="inventory-history-button" :aria-label="'Ver histórico de ' + item.nome" @click="showHistory(item.id)">Ver histórico <span>({{ item.historyCount }})</span>${arrowIcon}</button><span v-else class="inventory-muted">Sem histórico disponível</span></td>
        </tr>
        <tr v-if="!filteredEquipment.length"><td colspan="5" class="inventory-empty">{{ data.equipment.length ? 'Nenhum equipamento encontrado. Ajuste a busca ou a situação da máquina.' : 'Nenhum equipamento disponível.' }}</td></tr>
      </tbody>
    </table>
  </div>
  <p class="inventory-footnote">O histórico mostra somente as ordens disponíveis para seu perfil.</p>
</section>
<section class="inventory-section" aria-labelledby="inventory-parts-title">
  <header class="inventory-section-heading">
    <div><h2 id="inventory-parts-title">Peças em estoque <span class="inventory-count">{{ data.parts.length }}</span></h2><p>Quantidades disponíveis e identificação das peças críticas.</p></div>
    <label class="inventory-search" for="inventory-parts-search">Buscar peça<span>${searchIcon}<input id="inventory-parts-search" type="search" placeholder="Código ou nome da peça" v-model="partSearch" /></span></label>
  </header>
  <div class="inventory-results"><p role="status" aria-live="polite" aria-atomic="true">{{ filteredParts.length }} de {{ data.parts.length }} peça(s)</p></div>
  <p class="inventory-scroll-hint">Deslize a tabela para ver todas as colunas →</p>
  <div class="inventory-table-scroll" role="region" aria-labelledby="inventory-parts-title" tabindex="0">
    <table class="inventory-table">
      <thead><tr><th scope="col">Código</th><th scope="col">Peça</th><th scope="col" class="inventory-numeric">Estoque (un.)</th><th scope="col">Criticidade</th></tr></thead>
      <tbody>
        <tr v-for="item in filteredParts" :key="item.id">
          <td class="inventory-code">{{ item.codigo }}</td><th scope="row">{{ item.nome }}</th>
          <td class="inventory-numeric"><strong :class="{ 'inventory-out-of-stock': item.quantidadeEstoque === 0 }">{{ item.quantidadeEstoque }}</strong><small v-if="item.quantidadeEstoque === 0">Sem estoque</small></td>
          <td><span class="inventory-tag" :class="{ 'is-critical': item.critica }">{{ item.critica ? 'Crítica' : 'Não crítica' }}</span></td>
        </tr>
        <tr v-if="!filteredParts.length"><td colspan="4" class="inventory-empty">{{ data.parts.length ? 'Nenhuma peça encontrada. Tente outro código ou nome.' : 'Nenhuma peça disponível.' }}</td></tr>
      </tbody>
    </table>
  </div>
</section>`;

export function mountOrUpdate(element, payload, receiver) {
    const existing = applications.get(element);
    if (existing) {
        Object.assign(existing.data, payload);
        return;
    }

    const data = reactive(payload);
    const app = createApp({
        setup() {
            const equipmentSearch = ref('');
            const equipmentStatus = ref('');
            const partSearch = ref('');
            const filteredEquipment = computed(() => data.equipment.filter(item =>
                matches(equipmentSearch.value, item.nome, item.codigoPatrimonio, item.setor) &&
                (!equipmentStatus.value || item.ativo === (equipmentStatus.value === 'active'))).sort(byName));
            const filteredParts = computed(() => data.parts.filter(item => matches(partSearch.value, item.codigo, item.nome)).sort(byName));
            return {
                data, equipmentSearch, equipmentStatus, partSearch, filteredEquipment, filteredParts,
                clearEquipmentFilters() { equipmentSearch.value = ''; equipmentStatus.value = ''; },
                showHistory(id) { return receiver.invokeMethodAsync('ShowHistory', id); }
            };
        },
        template
    });
    app.mount(element);
    applications.set(element, { app, data });

    // Blazor já pode ter removido o host quando DisposeAsync for executado.
    const observer = new MutationObserver(() => {
        if (element.isConnected) return;
        app.unmount();
        applications.delete(element);
        observer.disconnect();
    });
    observer.observe(document.body, { childList: true, subtree: true });
}
