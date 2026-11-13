BEGIN;
CREATE TABLE IF NOT EXISTS equipamento_operacao_diaria (
    equipamento_id uuid NOT NULL REFERENCES equipamentos(id) ON DELETE RESTRICT,
    data date NOT NULL,
    horas_operadas numeric(4,2) NOT NULL,
    CONSTRAINT pk_equipamento_operacao_diaria PRIMARY KEY (equipamento_id, data),
    CONSTRAINT ck_operacao_horas CHECK (horas_operadas >= 0 AND horas_operadas <= 24)
);
ALTER TABLE equipamento_operacao_diaria ENABLE ROW LEVEL SECURITY;
COMMIT;
