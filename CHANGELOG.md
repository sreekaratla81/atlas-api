# Changelog

All notable changes to the Atlas API are documented in this file. See [README](README.md) for setup and [docs/](docs/) for API contract and schema.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Azure Service Bus eventing, outbox pattern, notification consumers
- Msg91NotificationProvider, NotificationOrchestrator
- Eventing/Service Bus implementation plan doc

### Changed
- OutboxMessage DTO: Topic/EntityId primary; AggregateType/AggregateId deprecated
- Smtp config: use __SET_VIA_ENV for Username/FromEmail

### Security
- Removed hardcoded email from appsettings
- EmailService contact block uses SmtpConfig.FromEmail
