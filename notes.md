# restaure db

- surrpimer l'ancienne DB
- créer une db vide
- gunzip -c <ancienne.sql.gz> | docker exec -i <container-database> psql -U postgres -d bonprof

gunzip -c <ancienne.sql.gz> | docker exec -i container-postgres_hexaplanning psql -U Lancelot -d hexaplanning_db

# note minio

run the command :
`docker exec minio sh -c 'mc alias set local http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" && mc anonymous set download local/bonprof/avatars && mc anonymous get local/bonprof/avatars'`
to set the accesibility public for the specific folder
