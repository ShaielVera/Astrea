# Firebase Emulator Suite (solo Auth por ahora) para desarrollo y tests E2E.
# Evita instalar Java y firebase-tools en cada máquina: solo hace falta Docker.
FROM eclipse-temurin:21-jre AS java

FROM node:24-bookworm-slim
COPY --from=java /opt/java/openjdk /opt/java/openjdk
ENV JAVA_HOME=/opt/java/openjdk \
    PATH="/opt/java/openjdk/bin:${PATH}"

RUN npm install --global firebase-tools@15.36.0 && npm cache clean --force

WORKDIR /firebase
COPY firebase.json .

EXPOSE 9099 4000
CMD ["firebase", "emulators:start", "--only", "auth", "--project", "demo-astrea"]
