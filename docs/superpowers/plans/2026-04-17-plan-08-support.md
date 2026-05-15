# Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Support module — Ticket aggregate with replies, Department entity, commands for creating/replying/closing tickets, admin assignment workflow, knowledge base article CRUD, and client + admin ticket controllers.

**Architecture:** Ticket is an AggregateRoot owning a private `_replies` collection of TicketReply entities; Department is a simple entity for categorizing tickets; TicketCreatedEvent triggers NotifyDepartmentHandler; KB articles are simple entities with CRUD operations; admin can assign tickets to staff members.

**Tech Stack:** C# 12, ASP.NET Core 8, EF Core 8 + Npgsql, Wolverine CQRS, FluentValidation, xUnit + FluentAssertions + Testcontainers.

---

## File Map

```
src/Innovayse.Domain/Support/
  TicketStatus.cs                                   ← enum: Open, AwaitingReply, Answered, Closed
  TicketPriority.cs                                 ← enum: Low, Medium, High
  TicketReply.cs                                    ← Entity (message, author, isStaffReply)
  Ticket.cs                                         ← AggregateRoot (owns _replies)
  Department.cs                                     ← Entity (name, email)
  KbArticle.cs                                      ← Entity (title, content, category, published)
  Events/TicketCreatedEvent.cs
  Events/TicketRepliedEvent.cs
  Events/TicketClosedEvent.cs
  Interfaces/ITicketRepository.cs
  Interfaces/IDepartmentRepository.cs
  Interfaces/IKbArticleRepository.cs

src/Innovayse.Application/Support/
  DTOs/TicketReplyDto.cs
  DTOs/TicketDto.cs
  DTOs/TicketListItemDto.cs
  DTOs/DepartmentDto.cs
  DTOs/KbArticleDto.cs
  Commands/CreateTicket/...
  Commands/ReplyToTicket/...
  Commands/AssignTicket/...
  Commands/CloseTicket/...
  Commands/CreateDepartment/...
  Commands/CreateKbArticle/...
  Commands/UpdateKbArticle/...
  Commands/DeleteKbArticle/...
  Queries/GetTicket/...
  Queries/ListTickets/...
  Queries/GetMyTickets/...
  Queries/GetDepartments/...
  Queries/ListKbArticles/...
  Queries/GetKbArticle/...
  Events/TicketCreatedHandler.cs

src/Innovayse.Infrastructure/Support/
  Configurations/TicketConfiguration.cs
  Configurations/TicketReplyConfiguration.cs
  Configurations/DepartmentConfiguration.cs
  Configurations/KbArticleConfiguration.cs
  TicketRepository.cs
  DepartmentRepository.cs
  KbArticleRepository.cs

src/Innovayse.API/Support/
  TicketsController.cs                              ← Admin: all operations
  MyTicketsController.cs                            ← Client: create + view own tickets
  DepartmentsController.cs                          ← Admin: CRUD
  KnowledgebaseController.cs                        ← Public: read articles
  KnowledgebaseAdminController.cs                   ← Admin: CRUD articles
```

---

## Task 1: Domain — Ticket Aggregate + Department + KbArticle

Create Ticket aggregate with status/priority enums, TicketReply entity, Department entity, KbArticle entity, events, and repository interfaces.

- [ ] **Step 1: Write domain tests for Ticket aggregate**
- [ ] **Step 2: Create enums (TicketStatus, TicketPriority)**
- [ ] **Step 3: Create TicketReply entity**
- [ ] **Step 4: Create Ticket aggregate with Create factory, AddReply, Assign, Close methods**
- [ ] **Step 5: Create Department and KbArticle entities**
- [ ] **Step 6: Create domain events**
- [ ] **Step 7: Create repository interfaces**
- [ ] **Step 8: Run tests to verify they pass**
- [ ] **Step 9: Run dotnet format and commit**

---

## Task 2: Application — DTOs

Create all DTOs for tickets, replies, departments, and KB articles.

---

## Task 3: Application — Ticket Commands

Create commands for CreateTicket, ReplyToTicket, AssignTicket, CloseTicket with handlers and validators.

---

## Task 4: Application — Department + KB Commands

Create commands for CreateDepartment, CreateKbArticle, UpdateKbArticle, DeleteKbArticle.

---

## Task 5: Application — Queries + Event Handler

Create queries for GetTicket, ListTickets, GetMyTickets, GetDepartments, ListKbArticles, GetKbArticle.

Create TicketCreatedHandler that sends notification to department.

---

## Task 6: Infrastructure — EF Core Persistence

Create configurations for Ticket, TicketReply, Department, KbArticle.
Create repositories.
Update AppDbContext and DependencyInjection.cs.
Create and apply migration.

---

## Task 7: API — Controllers

Create TicketsController (admin), MyTicketsController (client), DepartmentsController (admin), KnowledgebaseController (public), KnowledgebaseAdminController (admin).

---

## Task 8: Integration Tests

Create integration tests for ticket endpoints with proper auth checks.

---

## Self-Review

- [x] Ticket aggregate with replies
- [x] Department entity
- [x] KbArticle entity
- [x] All commands (Create, Reply, Assign, Close)
- [x] All queries
- [x] TicketCreatedHandler
- [x] EF Core persistence
- [x] API controllers
- [x] Integration tests

---

## Execution Handoff

Plan complete. Choose execution method.
