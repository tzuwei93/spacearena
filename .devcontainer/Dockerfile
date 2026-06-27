FROM unityci/editor:ubuntu-6000.5.0f1-webgl-3.2.2

# Install git, curl, python3, and basic tools
RUN apt-get update && apt-get install -y \
    git \
    curl \
    python3 \
    python3-pip \
    && rm -rf /var/lib/apt/lists/*
