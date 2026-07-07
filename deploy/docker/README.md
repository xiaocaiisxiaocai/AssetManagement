# Docker 部署文件

把整个仓库拉到服务器后，在仓库根目录执行：

```bash
cd ~/asset-management
cp deploy/docker/.env.example deploy/docker/.env
nano deploy/docker/.env
docker compose -f deploy/docker/docker-compose.yml --env-file deploy/docker/.env up -d --build
```

首次启动后查看状态：

```bash
docker compose -f deploy/docker/docker-compose.yml --env-file deploy/docker/.env ps
docker compose -f deploy/docker/docker-compose.yml --env-file deploy/docker/.env logs -f api
curl http://127.0.0.1/api/health
```

首次迁移和种子完成后，建议把 `deploy/docker/.env` 中这两项改成 `false`：

```env
DATABASE_AUTO_MIGRATE=false
DATABASE_AUTO_SEED=false
```

然后重启：

```bash
docker compose -f deploy/docker/docker-compose.yml --env-file deploy/docker/.env up -d
```

默认账号：`1001 / 123456`。首次登录后请修改密码。
