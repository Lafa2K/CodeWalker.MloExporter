# CodeWalker MLO Exporter

Ferramenta para abrir um `YTYP` ou `YTYP.XML` de MLO e exportar os props do interior, junto com os arquivos e texturas relacionados.

## Tutorial

1. Primeiro faça backup do seu mod.
2. Para props addon, crie um `RPF` com qualquer nome na pasta `mods` e copie o conteudo do mod addon para dentro dele.
3. Abra a ferramenta e espere ela carregar os arquivos do GTA.
4. Depois que o carregamento terminar, abra o `YTYP` onde esta o seu MLO.
5. Nao precisa converter para XML, embora a ferramenta tambem tenha capacidade de abrir `*.ytyp.xml`.
6. A ferramenta vai criar uma pasta com os arquivos exportados na mesma pasta onde esta o `YTYP`.

## Observacoes

- Em alguns casos a primeira carga pode demorar um pouco, principalmente quando existem props addon.
- A ferramenta tenta localizar modelos e texturas externas, inclusive em `mods`.
- O resultado da exportacao e salvo ao lado do arquivo original para facilitar a organizacao.
