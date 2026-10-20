using CaioFinance.Data;
using CaioFinance.Models;
using Microsoft.EntityFrameworkCore;

namespace CaioFinance.Services;

public class ServicoCategoria(CaioFinanceContexto db)
{
    public async Task<Categoria> GarantirOutrosAsync(CancellationToken cancellationToken = default)
    {
        var outros = await db.Categorias
            .FirstOrDefaultAsync(item => item.sNmCategoria == NomesPadrao.CategoriaOutros, cancellationToken);

        if (outros is not null)
            return outros;

        var ordem = await db.Categorias.MaxAsync(item => (int?)item.nNrOrdem, cancellationToken) ?? 0;
        outros = new Categoria
        {
            sNmCategoria = NomesPadrao.CategoriaOutros,
            nNrOrdem = ordem + 1
        };
        db.Categorias.Add(outros);
        await db.SaveChangesAsync(cancellationToken);
        return outros;
    }

    public async Task ExcluirComReatribuicaoAsync(int nCdCategoria, CancellationToken cancellationToken = default)
    {
        var categoria = await db.Categorias
            .FirstOrDefaultAsync(item => item.nCdCategoria == nCdCategoria, cancellationToken)
            ?? throw new InvalidOperationException("Categoria não encontrada.");

        if (categoria.sNmCategoria == NomesPadrao.CategoriaOutros)
            throw new InvalidOperationException("A categoria Outros não pode ser excluída.");

        var outros = await GarantirOutrosAsync(cancellationToken);
        if (outros.nCdCategoria == categoria.nCdCategoria)
            throw new InvalidOperationException("A categoria Outros não pode ser excluída.");

        await db.Lancamentos
            .Where(item => item.nCdCategoria == nCdCategoria)
            .ExecuteUpdateAsync(
                atualizacao => atualizacao.SetProperty(item => item.nCdCategoria, outros.nCdCategoria),
                cancellationToken);

        await db.LancamentosFuturos
            .Where(item => item.nCdCategoria == nCdCategoria)
            .ExecuteUpdateAsync(
                atualizacao => atualizacao.SetProperty(item => item.nCdCategoria, outros.nCdCategoria),
                cancellationToken);

        db.Categorias.Remove(categoria);
        await db.SaveChangesAsync(cancellationToken);
    }
}
