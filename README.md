# 🎮 Knup Xbox 360 Driver Suite

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20Windows-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![ViGEm](https://img.shields.io/badge/Emulation-ViGEmBus%20Native-orange?style=flat-square)](https://github.com/nefarius/ViGEmBus)
[![HidHide](https://img.shields.io/badge/Anti--Conflict-HidHide-blue?style=flat-square)](https://github.com/nefarius/HidHide)

Driver completo de sistema em segundo plano para conversão e emulação de controles genéricos **Knup / Twin USB / DragonRise** em controles nativos de **Xbox 360** no Windows, com suporte total a **Vibração Real (Dual Motor)**, **HidHide** (anti-clique duplo) e **Painel de Controle com Remapeamento de Botões**.

---

## 🌟 Principais Recursos

- **🚀 Driver Autônomo em Segundo Plano (Windows Service):**
  - Roda como um Serviço do Windows (`KnupDriverService`) com inicialização automática (`SYSTEM`).
  - Não depende de janelas abertas, consoles ou cliques manuais para funcionar — basta plugar o controle e jogar!
- **⚡ Emulação Xbox 360 100% Nativa (ViGEmBus):**
  - Reconhecido instantaneamente em jogos modernos da Steam, Epic Games, Game Pass, emuladores (RPCS3, Yuzu, PCSX2, Dolphin) e jogos antigos.
- **💥 Vibração Real nos Dois Motores (Force Feedback via HID Nativo):**
  - Comunicação de baixo nível via Win32 HID Output Reports.
  - Controle independente do **Motor Esquerdo (Pesado/Forte)** e **Motor Direito (Leve/Fraco)** sincronizados com as instruções XInput do jogo.
- **🙈 Proteção HidHide Integrada:**
  - Oculta o controle DirectInput original para evitar que jogos detectem dois controles ao mesmo tempo (problema clássico de clique duplo).
- **🎛️ Painel de Controle & Remapeador Visual (`KnupControlPanel.exe`):**
  - Interface gráfica opcional para remapear botões, calibrar analógicos e testar vibração.
  - **Recarregamento em Tempo Real:** Ao salvar no painel, o serviço em segundo plano atualiza o mapeamento instantaneamente via `FileSystemWatcher` sem interrupções.

---

## 🏗️ Arquitetura do Sistema

```mermaid
flowchart TD
    A[🎮 Controle Físico Knup / Twin USB] -->|DirectInput & Raw HID| B[⚙️ KnupDriverService (Windows Service)]
    B -->|ViGEmBus Virtual Bus| C[🟢 Controle Xbox 360 Virtual]
    C -->|XInput API| D[🕹️ Jogos & Emuladores]
    D -->|Feedback de Vibração| C
    C -->|FeedbackReceived| B
    B -->|SetOutputReport 0x01| A
    E[🎛️ KnupControlPanel GUI] <-->|ProgramData\\config.json| B
    F[🙈 HidHide Driver] -.->|Oculta dispositivo DirectInput| D
```

---

## 📥 Como Instalar

### 1. Pré-requisitos
Certifique-se de ter os drivers base instalados:
- **[ViGEmBus Driver](https://github.com/nefarius/ViGEmBus/releases)** (Necessário para a emulação do Xbox 360)
- **[HidHide Driver](https://github.com/nefarius/HidHide/releases)** (Recomendado para evitar conflito de controle duplo)

### 2. Instalação em 1 Clique (Recomendada)
1. Baixe o pacote **[`KnupDriver_v1.0.0_Setup.zip`](https://github.com/DiLucaYVL/KnupDriver/releases)** nas Releases.
2. Extraia o arquivo zip.
3. Clique com o botão direito em **`Instalar_Driver.bat`** e execute como **Administrador**.
4. O instalador irá:
   - Copiar os binários para `C:\Program Files\KnupDriver360`.
   - Registrar e iniciar o serviço `KnupDriverService`.
   - Criar o atalho do **Knup 360 Painel de Controle** na Área de Trabalho e Menu Iniciar.

---

## 🎮 Como Usar

- **Para Jogar:** Basta plugar seu controle USB e iniciar qualquer jogo. O driver já está rodando em segundo plano!
- **Para Remapear Botões ou Testar:**
  1. Abra o **`Knup 360 Painel de Controle`** pelo atalho da Área de Trabalho.
  2. Clique no botão que deseja alterar e pressione o botão correspondente no seu controle.
  3. Clique em **"💾 Salvar Configurações"**. O driver em segundo plano aplicará as mudanças na hora.

---

## 🔬 Especificações Técnicas do Protocolo HID

O controle Knup (chipset Twin USB / PantherLord, VID `0x0810` / PID `0x0001`) utiliza o seguinte descritor de Output Report para acionamento dos motores:

| Byte | Descrição | Valores |
| :---: | :--- | :---: |
| **0** | Report ID | `0x01` |
| **1** | Força do Motor Esquerdo (Pesado) | `0x00` a `0xFF` (0 - 255) |
| **2** | Força do Motor Direito (Leve) | `0x00` a `0xFF` (0 - 255) |
| **3** | Reservado | `0x00` |
| **4** | Flag de Ativação do Hardware (Ponte H) | `0xFF` (Ligado) / `0x00` (Desligado) |

---

## 🗑️ Como Desinstalar

Caso queira remover o driver:
1. Abra a pasta do instalador.
2. Execute **`Desinstalar_Driver.bat`** como **Administrador**.
3. O serviço será parado e removido do Windows juntamente com os atalhos.

---

## 🛠️ Tecnologias Utilizadas

- **C# / .NET 8.0 (Windows)**
- **Nefarius.ViGEm.Client** (Emulação de Xbox 360)
- **Nefarius.Drivers.HidHide** (Bloqueio de visibilidade HID)
- **SharpDX.DirectInput** (Captura de eixos analógicos e botões físicos)
- **HidSharp** & **P/Invoke Win32 HID API** (`HidD_SetOutputReport`)
- **Microsoft.Extensions.Hosting.WindowsServices** (Serviço de sistema)

---

## 📄 Licença

Este projeto é distribuído sob a licença [MIT](LICENSE).
