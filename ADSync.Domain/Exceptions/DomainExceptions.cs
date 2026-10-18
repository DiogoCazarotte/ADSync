namespace ADSync.Domain.Exceptions;

// Violação de uma regra de negócio (ex.: transição de status inválida, estoque insuficiente).
// A API traduz para HTTP 409 (Conflict).
public class RegraNegocioException : Exception
{
    public RegraNegocioException(string mensagem) : base(mensagem) { }
}

// Registro que não existe. A API traduz para HTTP 404.
public class NaoEncontradoException : Exception
{
    public NaoEncontradoException(string mensagem) : base(mensagem) { }
}

// Usuário logado sem permissão sobre aquele registro (ex.: técnico mexendo em O.S. de outro).
// A API traduz para HTTP 403.
public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string mensagem) : base(mensagem) { }
}