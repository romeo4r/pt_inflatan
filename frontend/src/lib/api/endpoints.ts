import { request, requestWithETag } from "./http";
import type {
  CreatePlayerRequest,
  CreateTeamRequest,
  MatchDetailResponse,
  MatchFilters,
  MatchResponse,
  PagedResult,
  PagingParams,
  PlayerFilters,
  PlayerResponse,
  RegisterMatchResultRequest,
  ScheduleMatchRequest,
  StandingsResponse,
  TeamFilters,
  TeamResponse,
  TopScorer,
} from "./types";

// Typed functions per resource. Components depend on these, never on URLs.

export const teamsApi = {
  list: (filters: TeamFilters = {}) =>
    request<PagedResult<TeamResponse>>("/api/teams", { query: { ...filters } }),

  get: (id: string) => requestWithETag<TeamResponse>(`/api/teams/${id}`),

  create: (body: CreateTeamRequest, idempotencyKey: string) =>
    request<TeamResponse>("/api/teams", { method: "POST", body, idempotencyKey }),
};

export const playersApi = {
  listByTeam: (teamId: string, filters: PlayerFilters = {}) =>
    request<PagedResult<PlayerResponse>>(`/api/teams/${teamId}/players`, { query: { ...filters } }),

  list: (filters: PlayerFilters & { teamId?: string } = {}) =>
    request<PagedResult<PlayerResponse>>("/api/players", { query: { ...filters } }),

  create: (teamId: string, body: CreatePlayerRequest, idempotencyKey: string) =>
    request<PlayerResponse>(`/api/teams/${teamId}/players`, { method: "POST", body, idempotencyKey }),
};

export const matchesApi = {
  list: (filters: MatchFilters = {}) =>
    request<PagedResult<MatchResponse>>("/api/matches", { query: { ...filters } }),

  get: (id: string) => requestWithETag<MatchDetailResponse>(`/api/matches/${id}`),

  schedule: (body: ScheduleMatchRequest, idempotencyKey: string) =>
    request<MatchDetailResponse>("/api/matches", { method: "POST", body, idempotencyKey }),

  registerResult: (id: string, body: RegisterMatchResultRequest, idempotencyKey: string, etag: string | null) =>
    request<MatchDetailResponse>(`/api/matches/${id}/result`, { method: "POST", body, idempotencyKey, etag }),
};

export const tournamentApi = {
  standings: () => request<StandingsResponse>("/api/standings"),

  topScorers: (params: PagingParams & { teamId?: string } = {}) =>
    request<PagedResult<TopScorer>>("/api/top-scorers", { query: { ...params } }),
};
