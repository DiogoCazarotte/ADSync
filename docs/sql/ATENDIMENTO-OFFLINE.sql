-- Revisar e executar uma única vez antes de habilitar Offline:Enabled.
-- Somente acrescenta a tabela de recibos; não altera ordens, estoque ou enums.
BEGIN;
CREATE TABLE os_eventos_sincronizados (
    id uuid PRIMARY KEY,
    ordem_servico_id uuid NOT NULL REFERENCES ordens_servico(id) ON DELETE RESTRICT,
    usuario_id uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT,
    ocorrido_em timestamp with time zone NOT NULL,
    hash character varying(64) NOT NULL,
    resposta text NOT NULL
);
CREATE INDEX "IX_os_eventos_sincronizados_ordem_servico_id_ocorrido_em"
    ON os_eventos_sincronizados(ordem_servico_id, ocorrido_em);
-- Tabela interna usada somente pela API; não publicar via REST anônimo.
ALTER TABLE os_eventos_sincronizados ENABLE ROW LEVEL SECURITY;
COMMIT;
