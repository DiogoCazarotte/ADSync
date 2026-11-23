using System.Security.Claims;
using ADSync.Api.Controllers;
using ADSync.Application.DTOs;
using ADSync.Application.Services;
using ADSync.Domain.Entities;
using ADSync.Domain.Enums;
using ADSync.Domain.Exceptions;
using ADSync.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

// Repositório em memória: não conecta nem altera qualquer banco.
var operador = Guid.NewGuid();
var tecnico = Guid.NewGuid();
var outro = Guid.NewGuid();
var propria = new OrdemServico { Id = Guid.NewGuid(), OperadorId = operador, TecnicoId = tecnico, Prioridade = PrioridadeOS.Alta };
var alheia = new OrdemServico { Id = Guid.NewGuid(), OperadorId = outro, TecnicoId = outro };
var semTecnico = new OrdemServico { Id = Guid.NewGuid(), OperadorId = outro };
var repo = new Orders([propria, alheia, semTecnico]);
var service = new OrdemServicoService(repo, null!, null!, null!, null!);
var controller = new OrdensServicoController(service);

void Login(Guid id, params string[] roles)
{
    var claims = new List<Claim> { new("sub", id.ToString()) };
    claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
    controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
}
void Check(bool value, string message) { if (!value) throw new Exception(message); }
async Task Denied(Func<Task> action)
{
    try { await action(); }
    catch (AcessoNegadoException) { return; }
    throw new Exception("Acesso indevido foi permitido.");
}
async Task<List<OrdemServicoDto>> List(bool? minhas = null)
{
    var result = minhas.HasValue ? await controller.Listar(null, null, minhas.Value) : await controller.Listar(null, null);
    return (List<OrdemServicoDto>)((OkObjectResult)result.Result!).Value!;
}

foreach (var (id, role) in new[] { (operador, "Operador"), (tecnico, "Tecnico") })
{
    Login(id, role);
    foreach (bool? minhas in new bool?[] { null, false, true })
    {
        var list = await List(minhas);
        Check(list.Count == 1 && list[0].Id == propria.Id, $"{role}: filtro minhas contornou o escopo.");
    }
    Check((await controller.ObterPorId(propria.Id)).Result is OkObjectResult, "Detalhe próprio bloqueado.");
    Check((await controller.ObterPorId(alheia.Id)).Result is NotFoundResult, "Detalhe alheio exposto.");
    Check((await controller.ObterPorId(semTecnico.Id)).Result is NotFoundResult, "Ordem sem vínculo exposta.");
    Check((await controller.ObterPorId(Guid.NewGuid())).Result is NotFoundResult, "Ordem inexistente não retornou 404.");
    var filtered = await controller.Listar(StatusOS.Aberta, PrioridadeOS.Alta);
    Check(((List<OrdemServicoDto>)((OkObjectResult)filtered.Result!).Value!).Count == 1, "Filtros não preservados.");
    Console.WriteLine($"PASS: {role}: escopo obrigatório; detalhe próprio; 404 para alheias/inexistentes; filtros preservados.");
}
Login(outro, "Gestor");
Check((await List()).Count == 3, "Gestor não consultou todas.");
Check((await controller.ObterPorId(propria.Id)).Result is OkObjectResult, "Gestor sem acesso ao detalhe.");
Login(outro, "Gestor", "Tecnico");
Check((await List(true)).Count == 3, "Perfil técnico restringiu o gestor.");
Console.WriteLine("PASS: gestor consulta todas, inclusive com papel adicional.");
Login(outro, "Desconhecido");
await Denied(async () => { await List(); });
await Denied(async () => { await controller.ObterPorId(propria.Id); });
Login(outro);
await Denied(async () => { await List(); });
controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Gestor")], "test"));
await Denied(async () => { await List(); });
await Denied(async () => { await controller.ObterPorId(propria.Id); });
Console.WriteLine("PASS: perfil ausente/desconhecido e identificador inválido são recusados.");

Login(tecnico, "Tecnico");
await Denied(async () => { await controller.Iniciar(semTecnico.Id, null); });
await Denied(async () => { await controller.Iniciar(alheia.Id, null); });
Check(semTecnico.TecnicoId is null && semTecnico.RegistrosTempo.Count == 0 && repo.Saves == 0, "Tentativa negada alterou a ordem.");
await controller.Iniciar(propria.Id, null);
Check(propria.Status == StatusOS.EmAndamento && repo.Saves == 1, "Técnico responsável não conseguiu iniciar.");
await Denied(async () => { await service.PausarAsync(propria.Id, outro, "Teste", null); });
Console.WriteLine("PASS: início exige atribuição prévia e mantém o atendimento do responsável.");

Login(operador, "Operador");
var saves = repo.Saves;
var originalStatus = propria.Status;
var originalTechnician = propria.TecnicoId;
var originalEvents = propria.RegistrosTempo.Count;
var edited = await controller.EditarDescricao(propria.Id, new() { DescricaoProblema = "  Vazamento corrigido na descrição  " });
Check(((OrdemServicoDetalheDto)((OkObjectResult)edited.Result!).Value!).DescricaoProblema == "Vazamento corrigido na descrição", "Descrição não atualizada na resposta.");
Check(propria.DescricaoProblema == "Vazamento corrigido na descrição" && repo.Saves == saves + 1, "Correção não persistida.");
Check(propria.Status == originalStatus && propria.TecnicoId == originalTechnician && propria.RegistrosTempo.Count == originalEvents, "Edição alterou o atendimento.");
await Denied(async () => { await controller.EditarDescricao(alheia.Id, new() { DescricaoProblema = "Alteração indevida" }); });
foreach (var text in new[] { "", "   ", new string('a', 2001) })
{
    try { await service.EditarDescricaoAsync(propria.Id, operador, false, text); throw new Exception("Descrição inválida aceita."); }
    catch (RegraNegocioException) { }
}
Check(repo.Saves == saves + 1 && alheia.DescricaoProblema == "", "Tentativa recusada salvou alterações.");
Login(outro, "Gestor");
await controller.EditarDescricao(propria.Id, new() { DescricaoProblema = new string('a', 2000) });
Check(propria.DescricaoProblema.Length == 2000, "Gestor não corrigiu OS de outro operador ou limite de 2000 incorreto.");
Console.WriteLine("PASS: operador corrige apenas suas OS; gestor corrige todas; validação e atendimento preservados.");

var scheduled = new OrdemServico { Id = Guid.NewGuid(), TecnicoId = tecnico, TipoManutencao = TipoManutencao.Preventiva, DataAbertura = DateTimeOffset.UtcNow.AddDays(1) };
var scheduledRepo = new Orders([scheduled]);
try
{
    await new OrdemServicoService(scheduledRepo, null!, null!, null!, null!).IniciarAsync(scheduled.Id, tecnico, DateTimeOffset.UtcNow.AddMinutes(4));
    throw new Exception("API permitiu iniciar preventiva futura.");
}
catch (RegraNegocioException) { }
Check(scheduled.Status == StatusOS.Aberta && scheduled.RegistrosTempo.Count == 0 && scheduledRepo.Saves == 0, "Início antecipado gravou dados.");
Console.WriteLine("PASS: início direto pela API também bloqueia preventiva futura sem gravar alterações.");

var services = new ServiceCollection();
services.AddLogging();
services.AddAuthorization();
using var provider = services.BuildServiceProvider();
var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
var authorization = provider.GetRequiredService<IAuthorizationService>();
foreach (var (type, action, allowedRoles) in new[]
{
    (typeof(OrdensServicoController), nameof(OrdensServicoController.EditarDescricao), new[] { "Operador", "Gestor" }),
    (typeof(UsuariosController), nameof(UsuariosController.AlterarSenha), new[] { "Gestor" })
})
{
    var attributes = type.GetCustomAttributes<AuthorizeAttribute>().Concat(type.GetMethod(action)!.GetCustomAttributes<AuthorizeAttribute>());
    var policy = (await AuthorizationPolicy.CombineAsync(policies, attributes))!;
    foreach (var role in new[] { "Operador", "Tecnico", "Gestor", "Desconhecido" })
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        Check((await authorization.AuthorizeAsync(user, null, policy)).Succeeded == allowedRoles.Contains(role), $"Permissão incorreta em {action} para {role}.");
    }
    Check(!(await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded, $"Anônimo autorizado em {action}.");
}
Console.WriteLine("PASS: políticas da API restringem edição da descrição e troca de senha aos perfis autorizados.");
foreach (var type in new[] { typeof(PecasController), typeof(PlanosPreventivosController) })
{
    foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes().Any(a => a is Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute)))
    {
        var attributes = type.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>());
        var policy = (await AuthorizationPolicy.CombineAsync(policies, attributes))!;
        foreach (var role in new[] { "Operador", "Tecnico", "Gestor" })
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
            var allowed = (await authorization.AuthorizeAsync(user, null, policy)).Succeeded;
            var isRead = method.GetCustomAttribute<HttpGetAttribute>() is not null;
            var startsPreventive = type == typeof(PlanosPreventivosController) && method.Name == nameof(PlanosPreventivosController.Iniciar);
            var expected = startsPreventive ? role == "Tecnico" : role == "Gestor" || role == "Tecnico" && isRead;
            Check(allowed == expected, $"Permissão incorreta em {type.Name}.{method.Name} para {role}.");
        }
    }
}
Console.WriteLine("PASS: técnico inicia preventivas; somente gestor altera planos e estoque; operador não acessa essas ações.");

sealed class Orders(List<OrdemServico> items) : IOrdemServicoRepository
{
    public int Saves { get; private set; }
    public Task<OrdemServico?> ObterPorIdAsync(Guid id) => Task.FromResult(items.FirstOrDefault(o => o.Id == id));
    public Task<OrdemServico?> ObterDetalhadoAsync(Guid id) => ObterPorIdAsync(id);
    public Task<OrdemServico?> ObterPorClientSyncIdAsync(Guid id) => Task.FromResult(items.FirstOrDefault(o => o.ClientSyncId == id));
    public Task<List<OrdemServico>> ListarAsync(StatusOS? status = null, PrioridadeOS? prioridade = null, Guid? tecnicoId = null, Guid? operadorId = null) => Task.FromResult(items.Where(o =>
        (!status.HasValue || o.Status == status) && (!prioridade.HasValue || o.Prioridade == prioridade) &&
        (!tecnicoId.HasValue || o.TecnicoId == tecnicoId) && (!operadorId.HasValue || o.OperadorId == operadorId)).ToList());
    public Task<string> ObterProximoNumeroAsync() => throw new NotSupportedException();
    public Task AdicionarAsync(OrdemServico os) => throw new NotSupportedException();
    public Task<OrdemServico> AdicionarComAtribuicaoAsync(OrdemServico os) => throw new NotSupportedException();
    public Task<bool> SalvarAlteracoesAsync() { Saves++; return Task.FromResult(true); }
}
