# Changelog: v0.5.0 → v0.5.1

**Release Date:** January 2, 2026

---

## Highlights

This patch release adds **Loki log aggregation** support for centralized logging and improves the observability documentation for production deployments.

---

## New Features

### Loki Log Aggregation Support
- Added `Serilog.Sinks.Grafana.Loki` package for sending logs directly to Loki
- Configured production settings to use Loki at `http://localhost:3100` for deployments where the bot runs outside Docker on the same host as the observability stack

---

## Improvements

### Observability Documentation
- **Nginx Reverse Proxy Setup** - Comprehensive section added to Jaeger/Loki setup documentation:
  - Step-by-step SSL certificate acquisition with Let's Encrypt
  - Subdomain-based routing for Grafana, Jaeger, and Loki
  - Alternative single-domain path-based routing configuration
  - Basic authentication for Jaeger and Loki endpoints
  - DNS and firewall configuration guidelines
  - Security hardening recommendations

### Deployment Configuration
- Updated `deployment/discordbot.env.template` with observability settings:
  - Loki sink configuration (`Serilog__WriteTo__2__*`)
  - Jaeger OTLP endpoint configuration
  - Clear documentation for localhost endpoints when running outside Docker

---

## Configuration Changes

### appsettings.Production.json
- Replaced Seq sink with Loki sink for log aggregation
- Updated OTLP endpoint from `http://jaeger:4317` to `http://localhost:4317` for non-Docker deployments

### Required Package
- `Serilog.Sinks.Grafana.Loki` (8.3.2)

---

## Technical Details

### Environment Variables for Observability

```bash
# Loki (Log Aggregation)
Serilog__WriteTo__2__Name=GrafanaLoki
Serilog__WriteTo__2__Args__uri=http://localhost:3100
Serilog__WriteTo__2__Args__labels__0__key=app
Serilog__WriteTo__2__Args__labels__0__value=discordbot
Serilog__WriteTo__2__Args__labels__1__key=environment
Serilog__WriteTo__2__Args__labels__1__value=production

# Jaeger (Distributed Tracing)
OpenTelemetry__Tracing__OtlpEndpoint=http://localhost:4317
OpenTelemetry__Tracing__OtlpProtocol=grpc
```

---

## Breaking Changes

None

---

## Contributors

- @cpike5
