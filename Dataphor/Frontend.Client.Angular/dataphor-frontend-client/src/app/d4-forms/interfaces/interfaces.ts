﻿import { EventEmitter } from '@angular/core';
import { IComponent, IDisposable } from './system-interfaces';
import { IAction } from './action-interfaces';
import { IReadOnly, ISourceReferenceChild } from './data-interfaces';
import { IVisual, HorizontalAlignment, VerticalAlignment } from './element-interfaces';
import { BehaviorSubject } from 'rxjs/BehaviorSubject';

export interface IMenu extends IActionNode { };

export interface IExposed extends IActionNode { };

export interface ISearchColumn extends INode, IReadOnly, ISourceReferenceChild {
    Title: string;
    Hint: string;
    ColumnName: string;
    Width: number;
    TextAlignment: HorizontalAlignment;
};

export interface IGridColumn extends INode, ISourceReferenceChild {
    Hint: string;
    Title: string;
    Width: number;
    Visible: boolean;
};

export interface ITriggerColumn extends IGridColumn {
    Text: string;
    Action: IAction;
};

export interface ISequenceColumn extends IGridColumn {
    Image: string;
    Script: string;
    ShouldEnlist: boolean;
};

export interface IDataGridColumn extends IGridColumn {
    ColumnName: string;
};

export interface ITextColumn extends IDataGridColumn {
    TextAlignment: HorizontalAlignment;
    MaxRows: number;
    VerticalAlignment: VerticalAlignment;
    WordWrap: boolean;
    VerticalText: boolean;
};

export interface IImageColumn extends IDataGridColumn {
    HorizontalAlignment: HorizontalAlignment;
    MaxRowHeight: number;
};

export interface ICheckBoxColumn extends IDataGridColumn, IReadOnly { };

export interface ISourceLink {
    SourceLinkType: SourceLinkType;
    SourceLink: SourceLink;
};

export interface ITimer extends INode {
    AutoReset: boolean;
    Enabled: boolean;
    Interval: number;
    OnElapsed: IAction;
    Start(): void;
    Stop(): void;
};

export interface IHost extends INode {
    Session: Session;
    Pipe: Pipe;
    NextRequest: Request;
    GetUniqueName(ANode: INode): void;
    Open(): void;
    Open(ADeferAfterActivate: boolean): void;
    AfterOpen(): void;
    Close(): void;
    Document: string;
    OnDocumentChanged$: BehaviorSubject<Object>; // event
    Load(ADocument: string, AInstance: Object): INode;
    LoadNext(AInstance: Object): INode;
};

export interface IChildCollection {
    // Sadly don't have collections as a non-property
    new (node: INode);
    Disown(AItem: INode): void;
    DisownAt(AIndex: number): INode;
};

export interface INode extends IDisposable, IComponent {
    Owner: INode;
    Parent: INode;
    Children: Array<INode>;
    //IsValidChild(AChild: INode, AChildType?: string): boolean;
    //IsValidOwner(AOwner?: INode, AOwnerType?: string): boolean;
    Host: IHost;
    //FindParent(AType: string): INode;
    OnValidateName: BehaviorSubject<string>; // event, rxjs
    GetNode(AName: string, AExcluding: INode): INode;
    GetNode(AName: string): INode;
    //FindNode(AName: string): INode;
    Transitional: boolean;
    Active: boolean;
    BroadcastEvent(AEvent: NodeEvent): void;
    HandleEvent(AEvent: NodeEvent): void;
    Name: string;
    UserData: Object;
};

export interface IModule { };

// public delegate void NodeEventHandler(INode ANode, EventParams AParams);

// Blocks changes until Node is finished changing
export interface IBlockable extends INode {
    OnCompleted$: BehaviorSubject<INode>; // event
};

export interface IEnableable {
    GetEnabled(): boolean;
    Enabled: boolean;
};

export interface IActionNode extends INode, IEnableable, IVisual {
    GetText(): string;
    Text: string;
    Action: IAction;
};

export interface INodeReference {
    Node: INode;
};

export class Request {
    private _document: string;
    Request(document: string): void {
        this._document = document;
    };
    get Document(): string { return this._document; }
    set Document(document: string) { this._document = document; }
};

export abstract class NodeEvent {
    IsHandled: boolean;
    abstract Handle(node: INode): void;
};

// delegate
// export interface NameChangeHandler {
//    (ASender: Object, AOldName: string, ANewName: string): void
// }

export interface INameChangeHandler {
    ASender: Object;
    AOldName: string;
    ANewName: string;
}