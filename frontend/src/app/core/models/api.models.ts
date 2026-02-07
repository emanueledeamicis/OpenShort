export interface Link {
    id?: number;
    slug: string;
    destinationUrl: string;
    domain: string;
    title?: string;
    notes?: string;
    isActive: boolean;
    redirectType: RedirectType;
    createdAt?: Date;
    visits?: number;
}

export enum RedirectType {
    Permanent = 301,
    Temporary = 302
}

export interface CreateLinkDto {
    destinationUrl: string;
    slug?: string;
    domain: string;
    title?: string;
    notes?: string;
    isActive?: boolean;
}

export interface UpdateLinkDto {
    destinationUrl: string;
    redirectType: RedirectType;
    title?: string;
    notes?: string;
    isActive: boolean;
}

export interface Domain {
    id: number;
    host: string;
    isActive: boolean;
}

export interface CreateDomainDto {
    host: string;
}
