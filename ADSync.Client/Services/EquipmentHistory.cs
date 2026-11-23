using ADSync.Application.DTOs;

namespace ADSync.Client.Services;

internal static class EquipmentHistory
{
    // Resumos de cache antigo sem ID não devem ser associados pelo nome.
    public static ILookup<Guid, OrdemServicoDto> Index(IEnumerable<OrdemServicoDto> orders) =>
        orders.Where(o => o.EquipamentoId != Guid.Empty).ToLookup(o => o.EquipamentoId);
}
