import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BrowserModule } from '@angular/platform-browser';
import { provideHttpClient } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { GameComponent } from '../game/game.component';
import { BoardComponent } from '../board/board.component';
import { CellComponent } from '../cell/cell.component';
import { GameControlsComponent } from '../game-controls/game-controls.component';

@NgModule({
  declarations: [
    AppComponent,
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    CommonModule,
    GameComponent,
    BoardComponent,
    GameControlsComponent,
    CellComponent,
  ],
  providers: [
    provideHttpClient()
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
