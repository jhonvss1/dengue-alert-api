namespace ApiAlertaDengue.Services.Cities;

public interface ICityCatalogService
{
    /// <summary>
    /// Carrega/atualiza o cadastro de municípios a partir do IBGE (insere novos, atualiza nome e UF).
    /// Não altera o acompanhamento (IsTracked) das cidades existentes. Retorna quantas cidades foram inseridas.
    /// </summary>
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}
