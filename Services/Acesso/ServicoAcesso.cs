using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CaioFinance.Services.Acesso;

public class ServicoAcesso(IOptions<OpcoesAcesso> opcoes, IMemoryCache cache)
{
    private const int TentativasAteBloquear = 8;
    private static readonly TimeSpan Bloqueio = TimeSpan.FromMinutes(15);

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(opcoes.Value.Usuario) && !string.IsNullOrWhiteSpace(opcoes.Value.Senha);

    public bool TentarEntrar(string? usuario, string? senha, string origem, out string? erro)
    {
        erro = null;
        if (!Configurado)
        {
            erro = "Defina o usuário e a senha em Acesso, no arquivo local de configuração.";
            return false;
        }

        var chave = "login:" + origem;
        if (cache.TryGetValue<Estado>(chave, out var estado) && estado is not null && estado.LiberadoEm > DateTimeOffset.UtcNow)
        {
            erro = "Muitas tentativas. Espere alguns minutos e tente de novo.";
            return false;
        }

        if (Confere(usuario, senha))
        {
            cache.Remove(chave);
            return true;
        }

        estado ??= new Estado();
        estado.Falhas++;
        if (estado.Falhas >= TentativasAteBloquear)
        {
            estado.Falhas = 0;
            estado.LiberadoEm = DateTimeOffset.UtcNow.Add(Bloqueio);
            erro = "Muitas tentativas. Espere alguns minutos e tente de novo.";
        }
        else
        {
            erro = "Usuário ou senha incorretos.";
        }

        cache.Set(chave, estado, Bloqueio);
        return false;
    }

    private bool Confere(string? usuario, string? senha)
    {
        var esperadoUsuario = Normalizar(opcoes.Value.Usuario);
        var informadoUsuario = Normalizar(usuario);
        var usuarioOk = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(esperadoUsuario)),
            SHA256.HashData(Encoding.UTF8.GetBytes(informadoUsuario)));

        var senhaOk = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(opcoes.Value.Senha)),
            SHA256.HashData(Encoding.UTF8.GetBytes(senha ?? "")));

        return usuarioOk && senhaOk;
    }

    private static string Normalizar(string? texto) => (texto ?? "").Trim().ToUpperInvariant();

    private sealed class Estado
    {
        public int Falhas { get; set; }
        public DateTimeOffset LiberadoEm { get; set; }
    }
}
