export interface Link {
    id?: number;
    slug: string;
    destinationUrl: string;
    domain: string;
    title?: string;
    notes?: string;
    isActive: boolean;
    createdAt?: Date;
    visits?: number;
}

export interface CreateLinkDto {
    destinationUrl: string;
    slug?: string;
    domain: string;
    title?: string;
    notes?: string;
}

export interface Domain {
    id: number;
    host: string;
    isActive: boolean;
}

export interface CreateDomainDto {
    host: string;
}
