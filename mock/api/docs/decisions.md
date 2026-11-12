# Decisions

- **Own project, not part of the gateway or core-api.** It must exercise the same path a real project API takes (platform-kit, project tokens, private network).
- **Forwarded headers trusted from 172.16.0.0/12 only.** Docker networks use that range, and only the gateway can reach this API.
