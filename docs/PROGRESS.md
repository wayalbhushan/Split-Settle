# Project Progress

## Phase 2: Groups and Members API
- Status: done
- Date: 2026-10-24
- Test totals: 43 passed, 0 failed
- Last commit hash: b850b1f
- Notes: Implemented Groups and Members controllers, DTOs, scoped services, ProblemDetails global exception handler, Decision 10, and WebApplicationFactory integration test suite.
- Needs from me: none

## Phase 3: Expenses, Balances, Settlements
- Status: done
- Date: 2026-10-24
- Test totals: 51 passed, 0 failed
- Last commit hash: cc617d5
- Notes: Implemented Expenses, Balances, and Settlements controllers, DTOs, scoped services, input validation, zero-sum precondition check, tools/golden/make_golden.py generator, 3 golden datasets, and verified exact match against real Django calculations.
- Needs from me: none

## Phase 4: Complete the Integration Test Suite
- Status: done
- Date: 2026-10-24
- Test totals: 62 passed, 0 failed
- Last commit hash: 9959ca7
- Notes: Ported 4 Django API tests as real HTTP integration tests, created docs/TEST_MATRIX.md, added database cascade and foreign key constraint tests, and verified 4 consecutive clean green test runs including after test database drop.
- Needs from me: none

## Phase 5: React Client against the .NET API
- Status: done
- Date: 2026-10-24
- Test totals: 62 passed, 0 failed
- Last commit hash: 85e6c95
- Notes: Wired client/src/api.js with VITE_API_BASE_URL, enabled CORS in .NET API, verified build and lint, documented all 12 client calls in docs/CLIENT_CALLS.md, verified manual checklist in docs/MANUAL_TEST.md, and validated all user flows in browser.
- Needs from me: none

## Phase 6: Generative AI Parse Feature
- Status: done
- Date: 2026-10-24
- Test totals: 75 passed, 0 failed
- Last commit hash: 506c0f9
- Notes: Implemented ILlmClient in Core, LlmClient with Gemini and Groq support, ParseService with prompt safety delimiters and strict JSON validation, POST parse endpoint, eval dataset with 20 sentences including 4 hard cases, fake LLM test suite, NOT MEASURED in docs/AI_EVAL.md, and React natural language parse box with draft preview.
- Needs from me: none

## Phase 7: Docker, CI, Configuration
- Status: done
- Date: 2026-10-24
- Test totals: 75 passed, 0 failed
- Last commit hash: pending
- Notes: Implemented multi-stage Dockerfile with non-root user, .dockerignore, startup migrations with Decision 11, EF Core transient error retry, GitHub Actions ci.yml workflow, updated README with architecture diagram, and verified container locally against database answering /health and /api/groups/.
- Needs from me: GitHub repository URL (at stop gate S4)

## Phase 8: Deployment
- Status: not started
- Date: 
- Test totals: 
- Last commit hash: 
- Notes: 
- Needs from me: Neon, Render, and Vercel accounts and live URLs (at stop gate S5)

## Phase 9: Final Documentation
- Status: not started
- Date: 
- Test totals: 
- Last commit hash: 
- Notes: 
- Needs from me: Screenshots and review (at stop gate S6)
