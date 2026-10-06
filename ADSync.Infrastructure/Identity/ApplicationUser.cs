using Microsoft.AspNetCore.Identity;

namespace ADSync.Infrastructure.Identity;

// Classe do Identity: cuida só de login, senha e segurança.
// Os dados de negócio (nome, CPF, tipo de usuário) ficam na entidade Usuario,
// que compartilha o mesmo Id desta classe.
//Por favor não mexer nisso!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
public class ApplicationUser : IdentityUser<Guid>
{
}
