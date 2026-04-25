export interface HttpHeader {
  key: string;
  value: string;
}

export interface SkillHttpConfig {
  method: string;
  url: string;
  headers: Record<string, string>;
  body: string;
}

export interface ScrapeFieldDef {
  name: string;
  selector: string;
  attribute: string;
}

export interface SkillScrapeConfig {
  selector: string;
  fields: ScrapeFieldDef[];
}

export interface SkillInfo {
  fileName: string;
  name: string;
  description: string;
  command: string;
  mode: string;
  parameterCount: number;
  skillType?: string;
  subSkillCount?: number;
  allowedTools?: string[];
  model?: string;
}

export interface SkillsResponse {
  success: boolean;
  data: SkillInfo[];
}

export interface SkillDetailResponse {
  success: boolean;
  data: {
    fileName: string;
    content: string;
    skillType?: string;
  };
}

export interface SkillParam {
  name: string;
  type: string;
  description: string;
  required: boolean;
}

export interface SubSkillJson {
  name: string;
  description: string;
  parameters: SkillParam[];
  command?: string;
  mode?: string;
  http?: SkillHttpConfig;
  scrape?: SkillScrapeConfig;
}

export interface SkillJson {
  name: string;
  description: string;
  parameters: SkillParam[];
  command?: string;
  mode?: string;
  http?: SkillHttpConfig;
  scrape?: SkillScrapeConfig;
  subSkills?: SubSkillJson[];
}
