FROM unityci/editor:ubuntu-6000.5.0f1-webgl-3.2.2

# Install tools needed by Codespaces, Unity batch builds, Codex CLI, and Unity MCP.
RUN apt-get update && apt-get install -y \
    ca-certificates \
    curl \
    git \
    gnupg \
    jq \
    less \
    libgtk-3-0 \
    libnss3 \
    libxss1 \
    openssh-client \
    python3 \
    python3-venv \
    unzip \
    xvfb \
    && rm -rf /var/lib/apt/lists/*

# Codex CLI is installed first so post-create setup can rely on it immediately.
RUN curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get update \
    && apt-get install -y nodejs \
    && npm install -g @openai/codex \
    && npm cache clean --force \
    && rm -rf /var/lib/apt/lists/*

# Unity MCP launches its Python server through uvx.
RUN curl -LsSf https://astral.sh/uv/install.sh | UV_INSTALL_DIR=/usr/local/bin sh \
    && uv --version \
    && uvx --version \
    && codex --version
