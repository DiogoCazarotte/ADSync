using ADSync.Application.DTOs;
using ADSync.Application.Interfaces;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Domain.Interfaces;

namespace ADSync.Application.Services;

// =====================================================================
//  Equipamentos
// =====================================================================
public class EquipamentoService : IEquipamentoService
{
    private readonly IEquipamentoRepository _repo;
    public EquipamentoService(IEquipamentoRepository repo) => _repo = repo;

    public async Task<List<EquipamentoDto>> ListarAsync(bool incluirInativos, string? setor)
    {
        var lista = await _repo.ListarAsync(incluirInativos, setor);
        return lista.Select(Mapear).ToList();
    }

    public async Task<EquipamentoDto?> ObterAsync(Guid id)
    {
        var e = await _repo.ObterPorIdAsync(id);
        return e is null ? null : Mapear(e);
    }

    public async Task<EquipamentoDto> CriarAsync(SalvarEquipamentoDto dto)
    {
        var codigo = dto.CodigoPatrimonio.Trim();

        if (await _repo.CodigoPatrimonioExisteAsync(codigo))
            throw new RegraNegocioException($"Já existe um equipamento com o código de patrimônio '{codigo}'.");

        var e = new Equipamento
        {
            Nome = dto.Nome.Trim(),
            CodigoPatrimonio = codigo,
            Setor = dto.Setor.Trim(),
            Ativo = true
        };

        await _repo.AdicionarAsync(e);
        await _repo.SalvarAlteracoesAsync();
        return Mapear(e);
    }

    public async Task<EquipamentoDto> AtualizarAsync(Guid id, SalvarEquipamentoDto dto)
    {
        var e = await ObterOuFalharAsync(id);
        var codigo = dto.CodigoPatrimonio.Trim();

        if (await _repo.CodigoPatrimonioExisteAsync(codigo, ignorarId: id))
            throw new RegraNegocioException($"Já existe outro equipamento com o código de patrimônio '{codigo}'.");

        e.Nome = dto.Nome.Trim();
        e.CodigoPatrimonio = codigo;
        e.Setor = dto.Setor.Trim();

        await _repo.SalvarAlteracoesAsync();
        return Mapear(e);
    }

    // RF12: exclusão lógica. Nunca apagamos o registro, só marcamos como inativo.
    public async Task<EquipamentoDto> AlterarSituacaoAsync(Guid id, bool ativo)
    {
        var e = await ObterOuFalharAsync(id);

        if (e.Ativo == ativo)
            return Mapear(e);

        if (!ativo && await _repo.TemOrdensEmAbertoAsync(id))
            throw new RegraNegocioException(
                "Há ordens de serviço em aberto para este equipamento. Conclua ou cancele antes de inativar.");

        e.Ativo = ativo;
        await _repo.SalvarAlteracoesAsync();
        return Mapear(e);
    }

    private async Task<Equipamento> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Equipamento não encontrado.");

    private static EquipamentoDto Mapear(Equipamento e) => new()
    {
        Id = e.Id,
        Nome = e.Nome,
        CodigoPatrimonio = e.CodigoPatrimonio,
        Setor = e.Setor,
        Ativo = e.Ativo
    };
}

// =====================================================================
//  Causas de falha
// =====================================================================
public class CausaFalhaService : ICausaFalhaService
{
    private readonly ICausaFalhaRepository _repo;
    public CausaFalhaService(ICausaFalhaRepository repo) => _repo = repo;

    public async Task<List<CausaFalhaDto>> ListarAsync(bool incluirInativos)
    {
        var lista = await _repo.ListarAsync(incluirInativos);
        return lista.Select(Mapear).ToList();
    }

    public async Task<CausaFalhaDto?> ObterAsync(Guid id)
    {
        var c = await _repo.ObterPorIdAsync(id);
        return c is null ? null : Mapear(c);
    }

    public async Task<CausaFalhaDto> CriarAsync(SalvarCausaFalhaDto dto)
    {
        var descricao = dto.Descricao.Trim();

        if (await _repo.DescricaoExisteAsync(descricao))
            throw new RegraNegocioException($"Já existe uma causa de falha chamada '{descricao}'.");

        var c = new CausaFalha { Descricao = descricao, Ativo = true };

        await _repo.AdicionarAsync(c);
        await _repo.SalvarAlteracoesAsync();
        return Mapear(c);
    }

    public async Task<CausaFalhaDto> AtualizarAsync(Guid id, SalvarCausaFalhaDto dto)
    {
        var c = await ObterOuFalharAsync(id);
        var descricao = dto.Descricao.Trim();

        if (await _repo.DescricaoExisteAsync(descricao, ignorarId: id))
            throw new RegraNegocioException($"Já existe outra causa de falha chamada '{descricao}'.");

        c.Descricao = descricao;
        await _repo.SalvarAlteracoesAsync();
        return Mapear(c);
    }

    // RF12: inativar não apaga, então as O.S. antigas que usam essa causa continuam íntegras.
    public async Task<CausaFalhaDto> AlterarSituacaoAsync(Guid id, bool ativo)
    {
        var c = await ObterOuFalharAsync(id);

        if (c.Ativo != ativo)
        {
            c.Ativo = ativo;
            await _repo.SalvarAlteracoesAsync();
        }

        return Mapear(c);
    }

    private async Task<CausaFalha> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Causa de falha não encontrada.");

    private static CausaFalhaDto Mapear(CausaFalha c) => new()
    {
        Id = c.Id,
        Descricao = c.Descricao,
        Ativo = c.Ativo
    };
}

// =====================================================================
//  Peças
// =====================================================================
public class PecaService : IPecaService
{
    private readonly IPecaRepository _repo;
    public PecaService(IPecaRepository repo) => _repo = repo;

    public async Task<List<PecaDto>> ListarAsync(string? busca)
    {
        var lista = await _repo.ListarAsync(busca);
        return lista.Select(Mapear).ToList();
    }

    public async Task<PecaDto?> ObterAsync(Guid id)
    {
        var p = await _repo.ObterPorIdAsync(id);
        return p is null ? null : Mapear(p);
    }

    public async Task<PecaDto> CriarAsync(CriarPecaDto dto)
    {
        var codigo = dto.Codigo.Trim();

        if (await _repo.CodigoExisteAsync(codigo))
            throw new RegraNegocioException($"Já existe uma peça com o código '{codigo}'.");

        var p = new Peca
        {
            Codigo = codigo,
            Nome = dto.Nome.Trim(),
            QuantidadeEstoque = dto.QuantidadeInicial,
            Critica = dto.Critica
        };

        await _repo.AdicionarAsync(p);
        await _repo.SalvarAlteracoesAsync();
        return Mapear(p);
    }

    // Atualiza só código e nome. O estoque muda apenas por entrada (compra) ou por uso em O.S.,
    // para o saldo nunca ser sobrescrito por engano.
    public async Task<PecaDto> AtualizarAsync(Guid id, AtualizarPecaDto dto)
    {
        var p = await ObterOuFalharAsync(id);
        var codigo = dto.Codigo.Trim();

        if (await _repo.CodigoExisteAsync(codigo, ignorarId: id))
            throw new RegraNegocioException($"Já existe outra peça com o código '{codigo}'.");

        p.Codigo = codigo;
        p.Nome = dto.Nome.Trim();
        p.Critica = dto.Critica;

        await _repo.SalvarAlteracoesAsync();
        return Mapear(p);
    }

    public async Task<PecaDto> RegistrarEntradaAsync(Guid id, int quantidade)
    {
        var p = await ObterOuFalharAsync(id);

        p.AdicionarAoEstoque(quantidade);

        await _repo.SalvarAlteracoesAsync();
        return Mapear(p);
    }

    private async Task<Peca> ObterOuFalharAsync(Guid id) =>
        await _repo.ObterPorIdAsync(id) ?? throw new NaoEncontradoException("Peça não encontrada.");

    private static PecaDto Mapear(Peca p) => new()
    {
        Id = p.Id,
        Codigo = p.Codigo,
        Nome = p.Nome,
        QuantidadeEstoque = p.QuantidadeEstoque,
        Critica = p.Critica
    };
}

// =====================================================================
//  Usuários (listagem)
// =====================================================================
public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _repo;
    public UsuarioService(IUsuarioRepository repo) => _repo = repo;

    public async Task<List<UsuarioDto>> ListarAsync(TipoUsuario? tipo, bool incluirInativos)
    {
        var lista = await _repo.ListarAsync(tipo, incluirInativos);
        return lista.Select(u => new UsuarioDto
        {
            Id = u.Id,
            Nome = u.Nome,
            Email = u.Email,
            TipoUsuario = u.TipoUsuario.ToString(),
            Ativo = u.Ativo
        }).ToList();
    }
}