(() => {
    const raiz = document.getElementById("form-conexao");
    if (!raiz) return;

    const select = raiz.querySelector("[data-banco]");
    const conectar = raiz.querySelector("[data-conectar]");
    const overlay = document.querySelector("[data-carregando]");
    const texto = document.querySelector("[data-carregando-texto]");
    const spinner = document.querySelector("[data-spinner]");
    const fechar = document.querySelector("[data-fechar-carregando]");
    const bancos = JSON.parse(document.getElementById("bancos-json")?.textContent || "[]");

    const token = () => raiz.querySelector("[name=__RequestVerificationToken]")?.value
        || document.querySelector("[name=__RequestVerificationToken]")?.value
        || "";

    const mostrar = (mensagem, erro) => {
        if (!overlay || !texto) return;
        overlay.hidden = false;
        overlay.setAttribute("aria-hidden", "false");
        texto.textContent = mensagem;
        if (spinner) spinner.hidden = !!erro;
        if (fechar) fechar.hidden = !erro;
    };

    const esconder = () => {
        if (!overlay) return;
        overlay.hidden = true;
        overlay.setAttribute("aria-hidden", "true");
        if (fechar) fechar.hidden = true;
        if (spinner) spinner.hidden = false;
    };

    fechar?.addEventListener("click", esconder);

    const montar = () => {
        if (!select) return;
        select.replaceChildren();
        bancos.forEach((banco) => {
            select.append(new Option(banco.nome, String(banco.id)));
        });
        if (select.options.length === 1)
            select.selectedIndex = 0;
    };

    montar();

    const postar = async (handler, campos) => {
        const corpo = new URLSearchParams(campos);
        corpo.set("__RequestVerificationToken", token());
        const resposta = await fetch(`/conexao?handler=${handler}`, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": token(),
                "Accept": "application/json"
            },
            body: corpo
        });
        const json = await resposta.json().catch(() => ({}));
        if (!resposta.ok || json.ok === false)
            throw new Error(json.mensagem || "Não foi possível concluir a operação.");
        return json;
    };

    const idDoItem = (dados) => {
        if (!dados) return "";
        if (typeof dados.id === "string") return dados.id;
        if (dados.item && typeof dados.item.id === "string") return dados.item.id;
        if (dados.data?.item?.id) return dados.data.item.id;
        return "";
    };

    const importar = async (itemId) => {
        if (!itemId) {
            mostrar("A Pluggy não devolveu a conexão.", true);
            return;
        }

        mostrar("Buscando saldo e extrato dos últimos 12 meses…", false);
        try {
            await postar("Concluir", { sIdItem: itemId });
            window.location.reload();
        } catch (erro) {
            mostrar(erro.message, true);
        }
    };

    const abrirWidget = async ({ conectorId, itemId }) => {
        if (!window.PluggyConnect) {
            mostrar("Não foi possível carregar a janela da Pluggy.", true);
            return;
        }

        mostrar(itemId ? "Abrindo a reconexão…" : "Abrindo o banco…", false);
        let tratado = false;
        try {
            const tokenConexao = await postar("Token", itemId ? { sIdItem: itemId } : {});
            const opcoes = {
                connectToken: tokenConexao.accessToken,
                includeSandbox: !!tokenConexao.incluirSandbox,
                products: ["ACCOUNTS", "TRANSACTIONS"],
                language: "pt",
                onSuccess: (dados) => {
                    tratado = true;
                    importar(idDoItem(dados));
                },
                onError: (erro) => {
                    const id = idDoItem(erro?.data || erro);
                    if (id) {
                        tratado = true;
                        importar(id);
                        return;
                    }
                    if (tratado) return;
                    tratado = true;
                    mostrar(erro?.message || "Não foi possível conectar o banco.", true);
                },
                onClose: () => {
                    if (!tratado) esconder();
                }
            };

            if (conectorId) opcoes.selectedConnectorId = Number(conectorId);
            if (itemId) opcoes.updateItem = itemId;

            const widget = new window.PluggyConnect(opcoes);
            await widget.init();
            if (!tratado) esconder();
        } catch (erro) {
            mostrar(erro.message, true);
        }
    };

    conectar?.addEventListener("click", () => {
        const conectorId = select?.value;
        if (!conectorId) {
            mostrar("Escolha o banco.", true);
            return;
        }
        abrirWidget({ conectorId });
    });

    document.querySelectorAll("[data-reconectar]").forEach((botao) => {
        botao.addEventListener("click", () => {
            abrirWidget({ itemId: botao.getAttribute("data-item") });
        });
    });

    const apelido = document.getElementById("janela-apelido");
    apelido?.addEventListener("cancel", (evento) => evento.preventDefault());
    apelido?.querySelector("[data-fechar]")?.addEventListener("click", () => apelido.close());
    document.querySelectorAll("[data-apelido]").forEach((botao) => {
        botao.addEventListener("click", () => {
            const formulario = apelido.querySelector("form");
            formulario.elements.namedItem("nCdConta").value = botao.dataset.id;
            formulario.elements.namedItem("apelido").value = botao.dataset.apelidoValor || "";
            apelido.querySelector("[data-nome-banco]").textContent = botao.dataset.nome || "";
            apelido.showModal();
        });
    });

    document.querySelector("[data-atualizar-tudo]")?.addEventListener("submit", async (evento) => {
        evento.preventDefault();
        mostrar("Atualizando todos os bancos…", false);
        try {
            await postar("AtualizarTudo", {});
            window.location.reload();
        } catch (erro) {
            mostrar(erro.message, true);
        }
    });

    document.querySelectorAll("[data-atualizar]").forEach((form) => {
        form.addEventListener("submit", async (evento) => {
            evento.preventDefault();
            mostrar("Atualizando saldo e extrato…", false);
            try {
                const dados = new FormData(form);
                await postar("Atualizar", { nCdConta: dados.get("nCdConta") });
                window.location.reload();
            } catch (erro) {
                mostrar(erro.message, true);
            }
        });
    });
})();
