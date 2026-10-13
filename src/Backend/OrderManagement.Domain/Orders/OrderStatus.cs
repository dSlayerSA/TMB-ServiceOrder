namespace OrderManagement.Domain.Orders;

public enum OrderStatus
{
    Pendente = 0,
    Processando = 1,
    Finalizado = 2,

    /// <summary>The worker could not complete the order.</summary>
    Falhou = 3,

    /// <summary>The order was given up on before it could complete.</summary>
    Cancelado = 4
}

/// <summary>
/// How the worker should end an order once its processing time elapses. The
/// default is <see cref="Concluir"/>; the others exist so the pipeline can be
/// exercised against outcomes that are otherwise hard to reproduce on demand.
/// </summary>
public enum ProcessingOutcome
{
    Concluir = 0,
    Falhar = 1,
    Cancelar = 2
}
